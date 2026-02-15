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
/// Generic OpenAI-compatible LLM provider for TGI, LocalAI, LM Studio, vLLM, and similar backends.
/// </summary>
public sealed class OpenAiCompatibleProvider : ILlmProvider
{
    private readonly LlmProviderConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenAiCompatibleProvider> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Initializes a new <see cref="OpenAiCompatibleProvider"/>.</summary>
    public OpenAiCompatibleProvider(
        LlmProviderConfig config,
        IHttpClientFactory httpClientFactory,
        ILogger<OpenAiCompatibleProvider> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string ProviderId => _config.ProviderId;

    /// <inheritdoc />
    /// <summary>POSTs to /v1/chat/completions. Adds Authorization header if ApiKeyEnv is set.</summary>
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = CreateClient(request.Timeout);
            var req = NewRequest(HttpMethod.Post, "/v1/chat/completions",
                BuildBody(request, stream: false));

            using var response = await client.SendAsync(req, ct);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;
            var choice = root.GetProperty("choices")[0];
            var content = choice.GetProperty("message").GetProperty("content").GetString() ?? "";
            var finish = choice.TryGetProperty("finish_reason", out var fr) ? fr.GetString() : null;
            var model = root.TryGetProperty("model", out var m) ? m.GetString() : _config.Model;

            int ptTok = 0, cpTok = 0;
            if (root.TryGetProperty("usage", out var u))
            {
                ptTok = u.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
                cpTok = u.TryGetProperty("completion_tokens", out var cp) ? cp.GetInt32() : 0;
            }

            sw.Stop();
            var tps = sw.Elapsed.TotalSeconds > 0 ? cpTok / sw.Elapsed.TotalSeconds : (double?)null;
            return new LlmResponse
            {
                Content = content,
                Usage = new LlmUsage(ptTok, cpTok, ptTok + cpTok, tps),
                InferenceTime = sw.Elapsed, FinishReason = finish, ModelId = model
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI-compatible completion failed for {ProviderId}", ProviderId);
            throw;
        }
    }

    /// <inheritdoc />
    /// <summary>Streams from /v1/chat/completions via SSE. Handles data: [DONE] termination.</summary>
    public async IAsyncEnumerable<LlmTokenChunk> StreamAsync(
        LlmRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        HttpResponseMessage? httpResp = null;
        Stream? stream = null;
        StreamReader? reader = null;
        HttpClient? client = null;
        try
        {
            client = CreateClient(request.Timeout);
            var req = NewRequest(HttpMethod.Post, "/v1/chat/completions",
                BuildBody(request, stream: true));

            httpResp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            httpResp.EnsureSuccessStatusCode();
            stream = await httpResp.Content.ReadAsStreamAsync(ct);
            reader = new StreamReader(stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI-compatible streaming failed for {ProviderId}", ProviderId);
            reader?.Dispose(); stream?.Dispose(); httpResp?.Dispose(); client?.Dispose();
            throw;
        }

        var totalTok = 0;
        using (client) using (httpResp) await using (stream) using (reader)
        {
            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                string? line;
                try { line = await reader.ReadLineAsync(ct); }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading SSE from {ProviderId}", ProviderId);
                    yield break;
                }

                if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ", StringComparison.Ordinal))
                    continue;

                var data = line.AsMemory("data: ".Length);
                if (data.Span.Trim().SequenceEqual("[DONE]"))
                {
                    yield return new LlmTokenChunk("", true, new LlmUsage(0, totalTok, totalTok, null));
                    yield break;
                }

                LlmTokenChunk? chunk;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var root = doc.RootElement;
                    var choices = root.GetProperty("choices");
                    if (choices.GetArrayLength() == 0) continue;

                    var choice = choices[0];
                    var delta = choice.GetProperty("delta");
                    var token = delta.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                    var finReason = choice.TryGetProperty("finish_reason", out var fr)
                        && fr.ValueKind != JsonValueKind.Null ? fr.GetString() : null;
                    var done = finReason is not null;
                    if (!string.IsNullOrEmpty(token)) totalTok++;

                    LlmUsage? usage = null;
                    if (done && root.TryGetProperty("usage", out var u))
                    {
                        int pt = u.TryGetProperty("prompt_tokens", out var pv) ? pv.GetInt32() : 0,
                            cp = u.TryGetProperty("completion_tokens", out var cv) ? cv.GetInt32() : 0;
                        usage = new LlmUsage(pt, cp, pt + cp, null);
                    }
                    else if (done) usage = new LlmUsage(0, totalTok, totalTok, null);
                    chunk = new LlmTokenChunk(token, done, usage);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse SSE chunk from {ProviderId}", ProviderId);
                    continue;
                }

                yield return chunk;
                if (chunk.IsComplete) yield break;
            }
        }
    }

    /// <inheritdoc />
    /// <summary>Performs a minimal 1-token completion to verify backend is reachable and model is loaded.</summary>
    public async Task<LlmHealthStatus> CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            using var client = CreateClient(TimeSpan.FromSeconds(15));
            var body = new Dictionary<string, object>
            {
                ["model"] = _config.Model ?? "default",
                ["messages"] = new[] { new { role = "user", content = "hi" } },
                ["max_tokens"] = 1, ["temperature"] = 0.0
            };
            var req = NewRequest(HttpMethod.Post, "/v1/chat/completions", body);
            using var resp = await client.SendAsync(req, ct);
            var ok = resp.IsSuccessStatusCode;

            string? activeModel = _config.Model;
            if (ok)
            {
                try
                {
                    using var doc = await JsonDocument.ParseAsync(
                        await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                    if (doc.RootElement.TryGetProperty("model", out var m))
                        activeModel = m.GetString();
                }
                catch { /* best-effort */ }
            }
            return new LlmHealthStatus(ok, ok, activeModel, null, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed for {ProviderId}", ProviderId);
            return new LlmHealthStatus(false, false, null, null, null, null);
        }
    }

    /// <inheritdoc />
    /// <summary>GETs /v1/models to discover loaded models. Falls back to config defaults.</summary>
    public async Task<LlmModelInfo> GetModelInfoAsync(CancellationToken ct)
    {
        var modelId = _config.Model ?? "unknown";
        var ctxWin = _config.ContextWindowOverride ?? 4096;
        var loaded = false;
        try
        {
            using var client = CreateClient(TimeSpan.FromSeconds(10));
            var req = new HttpRequestMessage(HttpMethod.Get, $"{_config.BaseUrl}/v1/models");
            ApplyAuth(req);
            using var resp = await client.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(
                    await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                if (doc.RootElement.TryGetProperty("data", out var arr) && arr.GetArrayLength() > 0)
                {
                    if (arr[0].TryGetProperty("id", out var id))
                        modelId = id.GetString() ?? modelId;
                    loaded = true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch /v1/models from {ProviderId}", ProviderId);
        }
        return new LlmModelInfo(modelId, ctxWin, "unknown", 0, 0, loaded);
    }

    /// <inheritdoc />
    /// <summary>Checks health to verify model readiness. Returns true if healthy.</summary>
    public async Task<bool> EnsureModelLoadedAsync(string modelId, CancellationToken ct)
    {
        var h = await CheckHealthAsync(ct);
        return h.IsReachable && h.IsModelLoaded;
    }

    /// <summary>Builds the standard OpenAI chat completion request body.</summary>
    private Dictionary<string, object> BuildBody(LlmRequest request, bool stream)
    {
        var msgs = request.Messages.Select(m => new { role = m.Role, content = m.Content }).ToArray();
        var body = new Dictionary<string, object>
        {
            ["model"] = request.Model ?? _config.Model ?? "default",
            ["messages"] = msgs,
            ["max_tokens"] = request.MaxTokens > 0 ? request.MaxTokens : _config.DefaultMaxTokens,
            ["temperature"] = request.Temperature > 0 ? request.Temperature : _config.DefaultTemperature,
            ["top_p"] = request.TopP, ["stream"] = stream
        };
        if (request.StopSequences is { Count: > 0 }) body["stop"] = request.StopSequences;
        if (request.RepetitionPenalty.HasValue) body["frequency_penalty"] = request.RepetitionPenalty.Value;
        if (request.ResponseFormat == LlmResponseFormat.Json)
            body["response_format"] = new { type = "json_object" };
        if (stream) body["stream_options"] = new { include_usage = true };
        return body;
    }

    /// <summary>Creates an authorized HTTP request with JSON body.</summary>
    private HttpRequestMessage NewRequest(HttpMethod method, string path, object body)
    {
        var req = new HttpRequestMessage(method, $"{_config.BaseUrl}{path}")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json")
        };
        ApplyAuth(req);
        return req;
    }

    /// <summary>Applies Bearer authorization if ApiKeyEnv is configured.</summary>
    private void ApplyAuth(HttpRequestMessage req)
    {
        if (string.IsNullOrEmpty(_config.ApiKeyEnv)) return;
        var key = Environment.GetEnvironmentVariable(_config.ApiKeyEnv);
        if (!string.IsNullOrEmpty(key))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }

    /// <summary>Creates an HttpClient with the configured or specified timeout.</summary>
    private HttpClient CreateClient(TimeSpan? timeout = null)
    {
        var c = _httpClientFactory.CreateClient($"openai-compat-{ProviderId}");
        c.Timeout = timeout ?? TimeSpan.FromSeconds(_config.TimeoutSeconds);
        return c;
    }
}
