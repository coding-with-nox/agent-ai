using System.Net.Http.Json;
using System.Text.Json;
using Codex.Core.Interfaces;
using Codex.Core.Models.Llm;

namespace Codex.Infrastructure.Llm.Providers;

/// <summary>
/// llama.cpp server provider.
/// </summary>
public sealed class LlamaCppProvider : ILlmProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes provider.
    /// </summary>
    /// <param name="providerId">Provider id.</param>
    /// <param name="httpClient">HTTP client.</param>
    /// <param name="defaultModel">Default model.</param>
    public LlamaCppProvider(string providerId, HttpClient httpClient, string defaultModel)
    {
        ProviderId = providerId;
        DefaultModel = defaultModel;
        _httpClient = httpClient;
    }

    /// <inheritdoc/>
    public string ProviderId { get; }

    /// <inheritdoc/>
    public string DefaultModel { get; }

    /// <inheritdoc/>
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct)
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;
        string prompt = string.Join("\n", request.Messages.Select(m => $"{m.Role}: {m.Content}"));
        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/completion", new
        {
            prompt,
            temperature = request.Temperature,
            n_predict = request.MaxTokens,
            stream = false
        }, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        string content = json.RootElement.TryGetProperty("content", out JsonElement c) ? c.GetString() ?? string.Empty : string.Empty;
        int completion = json.RootElement.TryGetProperty("tokens_predicted", out JsonElement t) ? t.GetInt32() : 0;
        TimeSpan duration = DateTimeOffset.UtcNow - start;
        return new LlmResponse
        {
            Content = content,
            InferenceTime = duration,
            ModelId = DefaultModel,
            Usage = new LlmUsage(0, completion, completion, duration.TotalSeconds > 0 ? completion / duration.TotalSeconds : null)
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<LlmTokenChunk> StreamAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using HttpRequestMessage message = new(HttpMethod.Post, "/completion")
        {
            Content = JsonContent.Create(new
            {
                prompt = string.Join("\n", request.Messages.Select(m => $"{m.Role}: {m.Content}")),
                temperature = request.Temperature,
                n_predict = request.MaxTokens,
                stream = true
            }, options: JsonOptions)
        };

        using HttpResponseMessage response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        Stream stream = await response.Content.ReadAsStreamAsync(ct);
        await foreach (string payload in SseStreamReader.ReadDataAsync(stream, ct))
        {
            if (payload.Equals("[DONE]", StringComparison.Ordinal))
            {
                yield return new LlmTokenChunk(string.Empty, true);
                yield break;
            }

            using JsonDocument json = JsonDocument.Parse(payload);
            string token = json.RootElement.TryGetProperty("content", out JsonElement c) ? c.GetString() ?? string.Empty : string.Empty;
            bool stop = json.RootElement.TryGetProperty("stop", out JsonElement s) && s.GetBoolean();
            yield return new LlmTokenChunk(token, stop);
        }
    }

    /// <inheritdoc/>
    public async Task<LlmHealthStatus> CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync("/health", ct);
            bool ok = response.IsSuccessStatusCode;
            return new LlmHealthStatus(ok, ok, ok ? DefaultModel : null, null, null, null);
        }
        catch
        {
            return new LlmHealthStatus(false, false, null, null, null, null);
        }
    }

    /// <inheritdoc/>
    public async Task<LlmModelInfo> GetModelInfoAsync(CancellationToken ct)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync("/props", ct);
        response.EnsureSuccessStatusCode();
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        int context = json.RootElement.TryGetProperty("n_ctx", out JsonElement nctx) ? nctx.GetInt32() : 0;
        return new LlmModelInfo(DefaultModel, context, "unknown", 0, 0, true);
    }

    /// <inheritdoc/>
    public Task<bool> EnsureModelLoadedAsync(string modelId, CancellationToken ct)
    {
        return Task.FromResult(true);
    }
}
