using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Codex.Core.Interfaces;
using Codex.Core.Models.Llm;
using Microsoft.Extensions.Logging;

namespace Codex.Infrastructure.Llm.Providers;

/// <summary>
/// LLM provider for vLLM inference servers using the OpenAI-compatible HTTP API.
/// </summary>
public sealed class VllmProvider : ILlmProvider
{
    private readonly LlmProviderConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VllmProvider> _logger;

    /// <summary>Initializes a new <see cref="VllmProvider"/>.</summary>
    /// <param name="config">Provider configuration.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger instance.</param>
    public VllmProvider(LlmProviderConfig config, IHttpClientFactory httpClientFactory, ILogger<VllmProvider> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string ProviderId => _config.ProviderId;

    /// <inheritdoc />
    /// <summary>Sends a non-streaming chat completion request and returns the full response.</summary>
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = CreateHttpClient();
            using var content = new StringContent(BuildRequestBody(request, false), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync($"{_config.BaseUrl}/v1/chat/completions", content, ct);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            var choice = root.GetProperty("choices")[0];
            var msg = choice.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
            var finish = choice.TryGetProperty("finish_reason", out var fr) ? fr.GetString() : null;
            var modelId = root.TryGetProperty("model", out var m) ? m.GetString() : request.Model;
            sw.Stop();
            _logger.LogDebug("vLLM completion in {Elapsed}ms for {Model}", sw.ElapsedMilliseconds, modelId);
            return new LlmResponse
            {
                Content = msg, Usage = ParseUsage(root, sw.Elapsed),
                InferenceTime = sw.Elapsed, FinishReason = finish, ModelId = modelId
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "vLLM completion failed after {Elapsed}ms", sw.ElapsedMilliseconds);
            throw;
        }
    }

    /// <inheritdoc />
    /// <summary>Streams token chunks from the vLLM server via server-sent events.</summary>
    public async IAsyncEnumerable<LlmTokenChunk> StreamAsync(
        LlmRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        HttpClient? client = null; HttpResponseMessage? httpResp = null;
        Stream? stream = null; StreamReader? reader = null;
        try
        {
            client = CreateHttpClient();
            var httpReq = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}/v1/chat/completions")
            { Content = new StringContent(BuildRequestBody(request, true), Encoding.UTF8, "application/json") };
            httpResp = await client.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct);
            httpResp.EnsureSuccessStatusCode();
            stream = await httpResp.Content.ReadAsStreamAsync(ct);
            reader = new StreamReader(stream);
            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ", StringComparison.Ordinal)) continue;
                var data = line["data: ".Length..];
                if (data == "[DONE]") { yield return new LlmTokenChunk(string.Empty, true); yield break; }
                LlmTokenChunk? chunk = null;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var choices = doc.RootElement.GetProperty("choices");
                    if (choices.GetArrayLength() == 0) continue;
                    var choice = choices[0];
                    var token = choice.GetProperty("delta").TryGetProperty("content", out var c)
                        ? c.GetString() ?? "" : "";
                    var fin = choice.TryGetProperty("finish_reason", out var fr) && fr.ValueKind != JsonValueKind.Null
                        ? fr.GetString() : null;
                    LlmUsage? usage = fin is not null && doc.RootElement.TryGetProperty("usage", out var ue)
                        ? ParseUsageElement(ue) : null;
                    chunk = new LlmTokenChunk(token, fin is not null, usage);
                }
                catch (JsonException ex) { _logger.LogWarning(ex, "Failed to parse SSE chunk: {Data}", data); }
                if (chunk is not null) yield return chunk;
            }
        }
        finally { reader?.Dispose(); stream?.Dispose(); httpResp?.Dispose(); client?.Dispose(); }
    }

    /// <inheritdoc />
    /// <summary>Checks whether the vLLM server is reachable and has a model loaded.</summary>
    public async Task<LlmHealthStatus> CheckHealthAsync(CancellationToken ct)
    {
        var down = new LlmHealthStatus(false, false, null, null, null, null);
        try
        {
            using var client = CreateHttpClient();
            bool reachable;
            try
            {
                using var hr = await client.GetAsync($"{_config.BaseUrl}/health", ct);
                reachable = hr.IsSuccessStatusCode;
            }
            catch { return down; }
            string? activeModel = null; var loaded = false;
            try
            {
                using var mr = await client.GetAsync($"{_config.BaseUrl}/v1/models", ct);
                if (mr.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await mr.Content.ReadAsStringAsync(ct));
                    var arr = doc.RootElement.GetProperty("data");
                    if (arr.GetArrayLength() > 0) { activeModel = arr[0].GetProperty("id").GetString(); loaded = true; }
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to query vLLM /v1/models in health check"); }
            _logger.LogDebug("vLLM health: reachable={R}, loaded={L}, model={M}", reachable, loaded, activeModel);
            return new LlmHealthStatus(reachable, loaded, activeModel, null, null, null);
        }
        catch (Exception ex) { _logger.LogError(ex, "vLLM health check failed"); return down; }
    }

    /// <inheritdoc />
    /// <summary>Retrieves model metadata. Uses config context window override or defaults to 8192.</summary>
    public async Task<LlmModelInfo> GetModelInfoAsync(CancellationToken ct)
    {
        var fallback = new LlmModelInfo(_config.Model ?? "unknown", _config.ContextWindowOverride ?? 8192,
            "unknown", 0, 0, false);
        try
        {
            using var client = CreateHttpClient();
            using var response = await client.GetAsync($"{_config.BaseUrl}/v1/models", ct);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var arr = doc.RootElement.GetProperty("data");
            if (arr.GetArrayLength() == 0) { _logger.LogWarning("vLLM /v1/models returned empty"); return fallback; }
            var model = arr[0];
            var modelId = model.GetProperty("id").GetString() ?? "unknown";
            var ctx = _config.ContextWindowOverride ?? 8192;
            if (_config.ContextWindowOverride is null
                && model.TryGetProperty("max_model_len", out var mml) && mml.ValueKind == JsonValueKind.Number)
                ctx = mml.GetInt32();
            var quant = model.TryGetProperty("quantization", out var q) ? q.GetString() ?? "unknown" : "unknown";
            var pCnt = model.TryGetProperty("parameter_count", out var pc) && pc.ValueKind == JsonValueKind.Number
                ? pc.GetInt64() : 0L;
            var vram = model.TryGetProperty("vram_usage_mb", out var vr) && vr.ValueKind == JsonValueKind.Number
                ? vr.GetInt64() : 0L;
            _logger.LogDebug("vLLM model: id={Id}, ctx={Ctx}", modelId, ctx);
            return new LlmModelInfo(modelId, ctx, quant, pCnt, vram, true);
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to get model info from vLLM"); return fallback; }
    }

    /// <inheritdoc />
    /// <summary>Verifies the model is listed on the vLLM server. vLLM does not support pulling.</summary>
    public async Task<bool> EnsureModelLoadedAsync(string modelId, CancellationToken ct)
    {
        try
        {
            using var client = CreateHttpClient();
            using var response = await client.GetAsync($"{_config.BaseUrl}/v1/models", ct);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var arr = doc.RootElement.GetProperty("data");
            for (var i = 0; i < arr.GetArrayLength(); i++)
                if (string.Equals(arr[i].GetProperty("id").GetString(), modelId, StringComparison.OrdinalIgnoreCase))
                { _logger.LogDebug("Model {Model} is loaded on vLLM", modelId); return true; }
            _logger.LogWarning("Model {Model} not found on vLLM server", modelId);
            return false;
        }
        catch (Exception ex)
        { _logger.LogError(ex, "Failed to check model {Model} on vLLM", modelId); return false; }
    }

    /// <summary>Creates an <see cref="HttpClient"/> with timeout and optional Bearer auth.</summary>
    private HttpClient CreateHttpClient()
    {
        var client = _httpClientFactory.CreateClient($"vllm-{_config.ProviderId}");
        client.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
        if (!string.IsNullOrEmpty(_config.ApiKeyEnv))
        {
            var key = Environment.GetEnvironmentVariable(_config.ApiKeyEnv);
            if (!string.IsNullOrEmpty(key))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }
        return client;
    }

    /// <summary>Builds the OpenAI-compatible JSON request body for chat completions.</summary>
    private static string BuildRequestBody(LlmRequest request, bool stream)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("model", request.Model);
            w.WritePropertyName("messages");
            w.WriteStartArray();
            foreach (var msg in request.Messages)
            {
                w.WriteStartObject();
                w.WriteString("role", msg.Role);
                w.WriteString("content", msg.Content);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteNumber("temperature", (double)request.Temperature);
            w.WriteNumber("max_tokens", request.MaxTokens);
            w.WriteNumber("top_p", (double)request.TopP);
            w.WriteBoolean("stream", stream);
            if (request.StopSequences is { Count: > 0 })
            {
                w.WritePropertyName("stop");
                w.WriteStartArray();
                foreach (var s in request.StopSequences) w.WriteStringValue(s);
                w.WriteEndArray();
            }
            if (request.RepetitionPenalty.HasValue)
                w.WriteNumber("repetition_penalty", (double)request.RepetitionPenalty.Value);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>Parses usage from the root response element, computing tokens-per-second.</summary>
    private static LlmUsage ParseUsage(JsonElement root, TimeSpan elapsed)
    {
        if (!root.TryGetProperty("usage", out var u)) return new LlmUsage(0, 0, 0, null);
        var pt = u.TryGetProperty("prompt_tokens", out var p) ? p.GetInt32() : 0;
        var ct = u.TryGetProperty("completion_tokens", out var c) ? c.GetInt32() : 0;
        var tt = u.TryGetProperty("total_tokens", out var t) ? t.GetInt32() : 0;
        var tps = elapsed.TotalSeconds > 0 && ct > 0 ? Math.Round(ct / elapsed.TotalSeconds, 2) : (double?)null;
        return new LlmUsage(pt, ct, tt, tps);
    }

    /// <summary>Parses usage from a standalone usage JSON element.</summary>
    private static LlmUsage ParseUsageElement(JsonElement u)
    {
        var pt = u.TryGetProperty("prompt_tokens", out var p) ? p.GetInt32() : 0;
        var ct = u.TryGetProperty("completion_tokens", out var c) ? c.GetInt32() : 0;
        var tt = u.TryGetProperty("total_tokens", out var t) ? t.GetInt32() : 0;
        return new LlmUsage(pt, ct, tt, null);
    }
}
