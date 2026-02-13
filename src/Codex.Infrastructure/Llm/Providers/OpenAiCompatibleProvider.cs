using System.Net.Http.Json;
using System.Text.Json;
using Codex.Core.Interfaces;
using Codex.Core.Models.Llm;

namespace Codex.Infrastructure.Llm.Providers;

/// <summary>
/// Generic provider for OpenAI-compatible chat completion servers.
/// </summary>
public class OpenAiCompatibleProvider : ILlmProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes the provider.
    /// </summary>
    /// <param name="providerId">Provider id.</param>
    /// <param name="httpClient">HTTP client.</param>
    /// <param name="defaultModel">Default model.</param>
    /// <param name="apiKey">Optional API key.</param>
    public OpenAiCompatibleProvider(string providerId, HttpClient httpClient, string defaultModel, string? apiKey = null)
    {
        ProviderId = providerId;
        DefaultModel = defaultModel;
        _httpClient = httpClient;
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
        }
    }

    /// <inheritdoc/>
    public string ProviderId { get; }

    /// <inheritdoc/>
    public string DefaultModel { get; }

    /// <inheritdoc/>
    public virtual async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct)
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;
        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/v1/chat/completions", ToRequest(request, false), JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        JsonElement root = json.RootElement;
        JsonElement usage = root.GetProperty("usage");
        string content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        int prompt = usage.GetProperty("prompt_tokens").GetInt32();
        int completion = usage.GetProperty("completion_tokens").GetInt32();
        int total = usage.GetProperty("total_tokens").GetInt32();
        TimeSpan duration = DateTimeOffset.UtcNow - start;
        double tps = duration.TotalSeconds <= 0 ? 0 : completion / duration.TotalSeconds;

        return new LlmResponse
        {
            Content = content,
            FinishReason = root.GetProperty("choices")[0].GetProperty("finish_reason").GetString(),
            ModelId = root.GetProperty("model").GetString(),
            InferenceTime = duration,
            Usage = new LlmUsage(prompt, completion, total, tps)
        };
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<LlmTokenChunk> StreamAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using HttpRequestMessage message = new(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(ToRequest(request, true), options: JsonOptions)
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
            JsonElement root = json.RootElement;
            string token = root.GetProperty("choices")[0].GetProperty("delta").TryGetProperty("content", out JsonElement delta)
                ? delta.GetString() ?? string.Empty
                : string.Empty;
            yield return new LlmTokenChunk(token, false);
        }
    }

    /// <inheritdoc/>
    public virtual async Task<LlmHealthStatus> CheckHealthAsync(CancellationToken ct)
    {
        LlmRequest request = new()
        {
            Model = DefaultModel,
            Messages = [new LlmMessage("user", "ping")],
            MaxTokens = 1,
            Temperature = 0
        };

        try
        {
            _ = await CompleteAsync(request, ct);
            return new LlmHealthStatus(true, true, DefaultModel, null, null, null);
        }
        catch
        {
            return new LlmHealthStatus(false, false, null, null, null, null);
        }
    }

    /// <inheritdoc/>
    public virtual async Task<LlmModelInfo> GetModelInfoAsync(CancellationToken ct)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync("/v1/models", ct);
        response.EnsureSuccessStatusCode();
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        JsonElement model = json.RootElement.GetProperty("data")[0];
        string modelId = model.GetProperty("id").GetString() ?? DefaultModel;
        return new LlmModelInfo(modelId, 0, "unknown", 0, 0, true);
    }

    /// <inheritdoc/>
    public virtual Task<bool> EnsureModelLoadedAsync(string modelId, CancellationToken ct)
    {
        return Task.FromResult(true);
    }

    private static object ToRequest(LlmRequest request, bool stream)
    {
        return new
        {
            model = request.Model,
            messages = request.Messages.Select(x => new { role = x.Role, content = x.Content }),
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            top_p = request.TopP,
            repetition_penalty = request.RepetitionPenalty,
            stream
        };
    }
}
