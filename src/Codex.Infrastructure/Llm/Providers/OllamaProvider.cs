using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Codex.Core.Interfaces;
using Codex.Core.Models.Llm;
using Microsoft.Extensions.Logging;

namespace Codex.Infrastructure.Llm.Providers;

/// <summary>
/// LLM provider implementation that communicates with an Ollama inference server.
/// </summary>
public sealed class OllamaProvider : ILlmProvider
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly LlmProviderConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OllamaProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaProvider"/> class.
    /// </summary>
    /// <param name="config">Provider configuration containing host, port, and model settings.</param>
    /// <param name="httpClientFactory">Factory for creating named HTTP clients.</param>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    public OllamaProvider(LlmProviderConfig config, IHttpClientFactory httpClientFactory, ILogger<OllamaProvider> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string ProviderId => _config.ProviderId;

    /// <inheritdoc />
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var client = CreateClient();
            var httpResp = await client.PostAsJsonAsync(
                $"{_config.BaseUrl}/api/chat", BuildChatPayload(request, stream: false), JsonOpts, ct);
            httpResp.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(
                await httpResp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;
            var content = root.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
            var evalCount = root.TryGetProperty("eval_count", out var ec) ? ec.GetInt32() : 0;
            var promptEvalCount = root.TryGetProperty("prompt_eval_count", out var pec) ? pec.GetInt32() : 0;
            var evalDurationNs = root.TryGetProperty("eval_duration", out var ed) ? ed.GetInt64() : 0L;
            var totalDurationNs = root.TryGetProperty("total_duration", out var td) ? td.GetInt64() : 0L;
            double? tokensPerSec = evalDurationNs > 0 ? evalCount / (evalDurationNs / 1_000_000_000.0) : null;
            sw.Stop();

            return new LlmResponse
            {
                Content = content,
                Usage = new LlmUsage(promptEvalCount, evalCount, promptEvalCount + evalCount, tokensPerSec),
                InferenceTime = totalDurationNs > 0 ? TimeSpan.FromTicks(totalDurationNs / 100) : sw.Elapsed,
                FinishReason = "stop",
                ModelId = request.Model
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama CompleteAsync failed for model {Model}", request.Model);
            sw.Stop();
            return new LlmResponse
            {
                Content = string.Empty,
                Usage = new LlmUsage(0, 0, 0, null),
                InferenceTime = sw.Elapsed,
                FinishReason = "error",
                ModelId = request.Model
            };
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LlmTokenChunk> StreamAsync(
        LlmRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        HttpResponseMessage? httpResp = null;
        Stream? responseStream = null;
        StreamReader? reader = null;
        try
        {
            var client = CreateClient();
            var httpReq = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}/api/chat")
            {
                Content = JsonContent.Create(BuildChatPayload(request, stream: true), options: JsonOpts)
            };
            httpResp = await client.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct);
            httpResp.EnsureSuccessStatusCode();
            responseStream = await httpResp.Content.ReadAsStreamAsync(ct);
            reader = new StreamReader(responseStream);

            while (!reader.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(line)) continue;

                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var done = root.TryGetProperty("done", out var d) && d.GetBoolean();
                var token = root.TryGetProperty("message", out var msg)
                    ? msg.GetProperty("content").GetString() ?? string.Empty
                    : string.Empty;

                if (done)
                {
                    var evalCount = root.TryGetProperty("eval_count", out var ec) ? ec.GetInt32() : 0;
                    var promptEval = root.TryGetProperty("prompt_eval_count", out var pec) ? pec.GetInt32() : 0;
                    var evalNs = root.TryGetProperty("eval_duration", out var ed) ? ed.GetInt64() : 0L;
                    double? tps = evalNs > 0 ? evalCount / (evalNs / 1_000_000_000.0) : null;
                    yield return new LlmTokenChunk(token, true,
                        new LlmUsage(promptEval, evalCount, promptEval + evalCount, tps));
                    yield break;
                }
                yield return new LlmTokenChunk(token, false);
            }
        }
        finally
        {
            reader?.Dispose();
            responseStream?.Dispose();
            httpResp?.Dispose();
        }
    }

    /// <inheritdoc />
    public async Task<LlmHealthStatus> CheckHealthAsync(CancellationToken ct)
    {
        bool isReachable = false, isModelLoaded = false;
        string? activeModel = null;
        try
        {
            var client = CreateClient();
            isReachable = (await client.GetAsync($"{_config.BaseUrl}/", ct)).IsSuccessStatusCode;
            if (isReachable)
            {
                var tagsResp = await client.GetAsync($"{_config.BaseUrl}/api/tags", ct);
                if (tagsResp.IsSuccessStatusCode)
                {
                    using var doc = await JsonDocument.ParseAsync(
                        await tagsResp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                    if (doc.RootElement.TryGetProperty("models", out var models))
                    {
                        foreach (var m in models.EnumerateArray())
                        {
                            var name = m.GetProperty("name").GetString();
                            if (name is null) continue;
                            if (_config.Model is null ||
                                name.StartsWith(_config.Model, StringComparison.OrdinalIgnoreCase))
                            {
                                isModelLoaded = true;
                                activeModel = name;
                                break;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Ollama health check failed"); }
        return new LlmHealthStatus(isReachable, isModelLoaded, activeModel, null, null, null);
    }

    /// <inheritdoc />
    public async Task<LlmModelInfo> GetModelInfoAsync(CancellationToken ct)
    {
        var model = _config.Model ?? "unknown";
        try
        {
            var client = CreateClient();
            var resp = await client.PostAsJsonAsync($"{_config.BaseUrl}/api/show", new { model }, JsonOpts, ct);
            resp.EnsureSuccessStatusCode();
            using var doc = await JsonDocument.ParseAsync(
                await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;

            int contextWindow = 4096;
            string quantization = "unknown";
            long parameterCount = 0;

            if (root.TryGetProperty("parameters", out var paramStr) &&
                paramStr.ValueKind == JsonValueKind.String)
            {
                foreach (var line in paramStr.GetString()!.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = line.Trim();
                    if (!trimmed.StartsWith("num_ctx", StringComparison.OrdinalIgnoreCase)) continue;
                    var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && int.TryParse(parts[^1], out var ctx)) contextWindow = ctx;
                }
            }
            if (root.TryGetProperty("details", out var details))
            {
                if (details.TryGetProperty("quantization_level", out var ql))
                    quantization = ql.GetString() ?? "unknown";
                if (details.TryGetProperty("parameter_size", out var ps))
                    parameterCount = ParseParameterSize(ps.GetString() ?? string.Empty);
            }
            return new LlmModelInfo(model, contextWindow, quantization, parameterCount, 0, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get model info for {Model}", model);
            return new LlmModelInfo(model, 4096, "unknown", 0, 0, false);
        }
    }

    /// <inheritdoc />
    public async Task<bool> EnsureModelLoadedAsync(string modelId, CancellationToken ct)
    {
        try
        {
            var client = CreateClient();
            var resp = await client.PostAsJsonAsync(
                $"{_config.BaseUrl}/api/pull", new { model = modelId, stream = false }, JsonOpts, ct);
            resp.EnsureSuccessStatusCode();
            _logger.LogInformation("Model {Model} pulled successfully", modelId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pull model {Model}", modelId);
            return false;
        }
    }

    /// <summary>Builds the Ollama /api/chat JSON payload from the given request.</summary>
    private static Dictionary<string, object> BuildChatPayload(LlmRequest request, bool stream)
    {
        var messages = new List<Dictionary<string, string>>(request.Messages.Count);
        foreach (var m in request.Messages)
            messages.Add(new Dictionary<string, string> { ["role"] = m.Role, ["content"] = m.Content });

        var options = new Dictionary<string, object>
        {
            ["temperature"] = request.Temperature,
            ["num_predict"] = request.MaxTokens,
            ["top_p"] = request.TopP
        };
        if (request.RepetitionPenalty.HasValue)
            options["repeat_penalty"] = request.RepetitionPenalty.Value;
        if (request.StopSequences is { Count: > 0 })
            options["stop"] = request.StopSequences;

        var payload = new Dictionary<string, object>
        {
            ["model"] = request.Model, ["messages"] = messages,
            ["stream"] = stream, ["options"] = options
        };
        if (request.ResponseFormat == LlmResponseFormat.Json) payload["format"] = "json";
        return payload;
    }

    /// <summary>Creates a named <see cref="HttpClient"/> from the factory.</summary>
    private HttpClient CreateClient() => _httpClientFactory.CreateClient(ProviderId);

    /// <summary>Parses an Ollama parameter_size string like "7B" or "13B" into a raw count.</summary>
    private static long ParseParameterSize(string size)
    {
        if (string.IsNullOrWhiteSpace(size)) return 0;
        size = size.Trim().ToUpperInvariant();
        double multiplier = 1;
        if (size.EndsWith('B')) { multiplier = 1_000_000_000; size = size[..^1]; }
        else if (size.EndsWith('M')) { multiplier = 1_000_000; size = size[..^1]; }
        return double.TryParse(size, out var value) ? (long)(value * multiplier) : 0;
    }
}
