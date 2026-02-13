using System.Net.Http.Json;
using System.Text.Json;
using Codex.Core.Interfaces;
using Codex.Core.Models.Llm;

namespace Codex.Infrastructure.Llm.Providers;

/// <summary>
/// Ollama runtime provider.
/// </summary>
public sealed class OllamaProvider : ILlmProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes provider.
    /// </summary>
    /// <param name="providerId">Provider id.</param>
    /// <param name="httpClient">HTTP client.</param>
    /// <param name="defaultModel">Default model.</param>
    public OllamaProvider(string providerId, HttpClient httpClient, string defaultModel)
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
        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/chat", BuildRequest(request, false), JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        JsonElement root = json.RootElement;
        string content = root.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        int prompt = root.TryGetProperty("prompt_eval_count", out JsonElement p) ? p.GetInt32() : 0;
        int completion = root.TryGetProperty("eval_count", out JsonElement c) ? c.GetInt32() : 0;
        int total = prompt + completion;
        TimeSpan duration = DateTimeOffset.UtcNow - start;

        return new LlmResponse
        {
            Content = content,
            FinishReason = root.TryGetProperty("done_reason", out JsonElement reason) ? reason.GetString() : null,
            ModelId = root.TryGetProperty("model", out JsonElement model) ? model.GetString() : request.Model,
            InferenceTime = duration,
            Usage = new LlmUsage(prompt, completion, total, duration.TotalSeconds > 0 ? completion / duration.TotalSeconds : null)
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<LlmTokenChunk> StreamAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using HttpRequestMessage message = new(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(BuildRequest(request, true), options: JsonOptions)
        };

        using HttpResponseMessage response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        Stream stream = await response.Content.ReadAsStreamAsync(ct);
        await foreach (string payload in SseStreamReader.ReadDataAsync(stream, ct))
        {
            using JsonDocument json = JsonDocument.Parse(payload);
            JsonElement root = json.RootElement;
            string token = root.TryGetProperty("message", out JsonElement msg)
                ? msg.GetProperty("content").GetString() ?? string.Empty
                : string.Empty;
            bool done = root.TryGetProperty("done", out JsonElement doneEl) && doneEl.GetBoolean();
            yield return new LlmTokenChunk(token, done);
            if (done)
            {
                yield break;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<LlmHealthStatus> CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage ping = await _httpClient.GetAsync("/", ct);
            if (!ping.IsSuccessStatusCode)
            {
                return new LlmHealthStatus(false, false, null, null, null, null);
            }

            using HttpResponseMessage tags = await _httpClient.GetAsync("/api/tags", ct);
            bool loaded = tags.IsSuccessStatusCode;
            return new LlmHealthStatus(true, loaded, loaded ? DefaultModel : null, null, null, null);
        }
        catch
        {
            return new LlmHealthStatus(false, false, null, null, null, null);
        }
    }

    /// <inheritdoc/>
    public async Task<LlmModelInfo> GetModelInfoAsync(CancellationToken ct)
    {
        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/show", new { name = DefaultModel }, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        JsonElement root = json.RootElement;
        JsonElement details = root.TryGetProperty("details", out JsonElement det) ? det : default;
        string quant = details.ValueKind == JsonValueKind.Object && details.TryGetProperty("quantization_level", out JsonElement q)
            ? q.GetString() ?? "unknown"
            : "unknown";
        int context = details.ValueKind == JsonValueKind.Object && details.TryGetProperty("context_length", out JsonElement cl)
            ? cl.GetInt32()
            : 0;

        return new LlmModelInfo(DefaultModel, context, quant, 0, 0, true);
    }

    /// <inheritdoc/>
    public async Task<bool> EnsureModelLoadedAsync(string modelId, CancellationToken ct)
    {
        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/pull", new { name = modelId }, JsonOptions, ct);
        return response.IsSuccessStatusCode;
    }

    private static object BuildRequest(LlmRequest request, bool stream)
    {
        return new
        {
            model = request.Model,
            messages = request.Messages.Select(x => new { role = x.Role, content = x.Content }),
            stream,
            options = new { temperature = request.Temperature, top_p = request.TopP, num_predict = request.MaxTokens }
        };
    }
}
