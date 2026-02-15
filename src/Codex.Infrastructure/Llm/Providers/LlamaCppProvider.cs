using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Codex.Core.Interfaces;
using Codex.Core.Models.Llm;
using Microsoft.Extensions.Logging;

namespace Codex.Infrastructure.Llm.Providers;

/// <summary>
/// LLM provider for the llama.cpp HTTP server. Uses /completion, /health, /props, and /slots.
/// </summary>
public sealed class LlamaCppProvider : ILlmProvider
{
    private readonly LlmProviderConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LlamaCppProvider> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Initializes a new <see cref="LlamaCppProvider"/>.</summary>
    /// <param name="config">Provider configuration.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger instance.</param>
    public LlamaCppProvider(
        LlmProviderConfig config,
        IHttpClientFactory httpClientFactory,
        ILogger<LlamaCppProvider> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string ProviderId => _config.ProviderId;

    /// <inheritdoc />
    /// <summary>POSTs to /completion with ChatML-formatted prompt. Parses content, tokens, and timings.</summary>
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = CreateClient(request.Timeout);
            var body = BuildBody(FormatChatMl(request.Messages), request, stream: false);

            using var response = await client.PostAsJsonAsync($"{_config.BaseUrl}/completion", body, JsonOpts, ct);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;

            var content = root.GetProperty("content").GetString() ?? string.Empty;
            var compTok = root.TryGetProperty("tokens_predicted", out var tp) ? tp.GetInt32() : 0;
            var promptTok = root.TryGetProperty("tokens_evaluated", out var te) ? te.GetInt32() : 0;
            double? tps = root.TryGetProperty("timings", out var tim)
                && tim.TryGetProperty("predicted_per_second", out var pps) ? pps.GetDouble() : null;

            var finish = root.TryGetProperty("stop_type", out var sr) ? sr.GetString()
                : root.TryGetProperty("stopped_eos", out var se) && se.GetBoolean() ? "stop" : "length";

            sw.Stop();
            return new LlmResponse
            {
                Content = content,
                Usage = new LlmUsage(promptTok, compTok, promptTok + compTok, tps),
                InferenceTime = sw.Elapsed, FinishReason = finish, ModelId = _config.Model
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "llama.cpp completion failed for provider {ProviderId}", ProviderId);
            throw;
        }
    }

    /// <inheritdoc />
    /// <summary>POSTs to /completion with stream:true. Parses SSE data: lines for token content.</summary>
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
            var json = JsonSerializer.Serialize(
                BuildBody(FormatChatMl(request.Messages), request, stream: true), JsonOpts);
            var req = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}/completion")
            { Content = new StringContent(json, Encoding.UTF8, "application/json") };

            httpResp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            httpResp.EnsureSuccessStatusCode();
            stream = await httpResp.Content.ReadAsStreamAsync(ct);
            reader = new StreamReader(stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "llama.cpp streaming request failed for {ProviderId}", ProviderId);
            reader?.Dispose(); stream?.Dispose(); httpResp?.Dispose(); client?.Dispose();
            throw;
        }

        using (client) using (httpResp) await using (stream) using (reader)
        {
            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                string? line;
                try { line = await reader.ReadLineAsync(ct); }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading SSE from llama.cpp {ProviderId}", ProviderId);
                    yield break;
                }

                if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ", StringComparison.Ordinal))
                    continue;

                LlmTokenChunk? chunk;
                try
                {
                    using var doc = JsonDocument.Parse(line.AsMemory("data: ".Length));
                    var root = doc.RootElement;
                    var token = root.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                    var stopped = root.TryGetProperty("stop", out var s) && s.GetBoolean();
                    LlmUsage? usage = null;
                    if (stopped)
                    {
                        var ct2 = root.TryGetProperty("tokens_predicted", out var tp2) ? tp2.GetInt32() : 0;
                        var pt2 = root.TryGetProperty("tokens_evaluated", out var te2) ? te2.GetInt32() : 0;
                        double? tps = root.TryGetProperty("timings", out var tim)
                            && tim.TryGetProperty("predicted_per_second", out var pps) ? pps.GetDouble() : null;
                        usage = new LlmUsage(pt2, ct2, pt2 + ct2, tps);
                    }
                    chunk = new LlmTokenChunk(token, stopped, usage);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse SSE chunk from llama.cpp {ProviderId}", ProviderId);
                    continue;
                }

                yield return chunk;
                if (chunk.IsComplete) yield break;
            }
        }
    }

    /// <inheritdoc />
    /// <summary>GETs /health. Returns healthy when response contains {"status":"ok"}.</summary>
    public async Task<LlmHealthStatus> CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            using var client = CreateClient(TimeSpan.FromSeconds(10));
            using var resp = await client.GetAsync($"{_config.BaseUrl}/health", ct);
            if (!resp.IsSuccessStatusCode)
                return new LlmHealthStatus(false, false, null, null, null, null);

            using var doc = await JsonDocument.ParseAsync(
                await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var ok = string.Equals(
                doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null,
                "ok", StringComparison.OrdinalIgnoreCase);
            return new LlmHealthStatus(true, ok, _config.Model, null, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed for llama.cpp {ProviderId}", ProviderId);
            return new LlmHealthStatus(false, false, null, null, null, null);
        }
    }

    /// <inheritdoc />
    /// <summary>GETs /props for context window (n_ctx) and /slots to verify model is loaded.</summary>
    public async Task<LlmModelInfo> GetModelInfoAsync(CancellationToken ct)
    {
        var ctxWin = _config.ContextWindowOverride ?? 4096;
        var loaded = false;
        try
        {
            using var client = CreateClient(TimeSpan.FromSeconds(10));
            try
            {
                using var pr = await client.GetAsync($"{_config.BaseUrl}/props", ct);
                if (pr.IsSuccessStatusCode)
                {
                    using var doc = await JsonDocument.ParseAsync(
                        await pr.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                    if (doc.RootElement.TryGetProperty("default_generation_settings", out var dgs)
                        && dgs.TryGetProperty("n_ctx", out var nCtx))
                        ctxWin = _config.ContextWindowOverride ?? nCtx.GetInt32();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch /props from llama.cpp {ProviderId}", ProviderId);
            }
            try
            {
                using var sr = await client.GetAsync($"{_config.BaseUrl}/slots", ct);
                loaded = sr.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch /slots from llama.cpp {ProviderId}", ProviderId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetModelInfo failed for llama.cpp {ProviderId}", ProviderId);
        }

        return new LlmModelInfo(_config.Model ?? "unknown", ctxWin, "unknown", 0, 0, loaded);
    }

    /// <inheritdoc />
    /// <summary>llama.cpp loads models at startup; verifies health to confirm readiness.</summary>
    public async Task<bool> EnsureModelLoadedAsync(string modelId, CancellationToken ct)
    {
        var h = await CheckHealthAsync(ct);
        return h.IsReachable && h.IsModelLoaded;
    }

    /// <summary>Formats messages into a ChatML prompt string.</summary>
    private static string FormatChatMl(IReadOnlyList<LlmMessage> messages)
    {
        var sb = new StringBuilder();
        foreach (var m in messages)
            sb.Append("<|im_start|>").Append(m.Role).Append('\n').Append(m.Content).Append("<|im_end|>\n");
        sb.Append("<|im_start|>assistant\n");
        return sb.ToString();
    }

    /// <summary>Builds the /completion request body.</summary>
    private Dictionary<string, object> BuildBody(string prompt, LlmRequest req, bool stream)
    {
        var stops = new List<string> { "<|im_end|>" };
        if (req.StopSequences is { Count: > 0 }) stops.AddRange(req.StopSequences);
        var body = new Dictionary<string, object>
        {
            ["prompt"] = prompt,
            ["n_predict"] = req.MaxTokens > 0 ? req.MaxTokens : _config.DefaultMaxTokens,
            ["temperature"] = req.Temperature > 0 ? req.Temperature : _config.DefaultTemperature,
            ["top_p"] = req.TopP, ["stream"] = stream, ["stop"] = stops
        };
        if (req.RepetitionPenalty.HasValue) body["repeat_penalty"] = req.RepetitionPenalty.Value;
        return body;
    }

    /// <summary>Creates an HttpClient with the configured or specified timeout.</summary>
    private HttpClient CreateClient(TimeSpan? timeout = null)
    {
        var c = _httpClientFactory.CreateClient($"llama-cpp-{ProviderId}");
        c.Timeout = timeout ?? TimeSpan.FromSeconds(_config.TimeoutSeconds);
        return c;
    }
}
