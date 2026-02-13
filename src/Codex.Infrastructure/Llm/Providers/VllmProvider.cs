using System.Net;
using Codex.Core.Models.Llm;

namespace Codex.Infrastructure.Llm.Providers;

/// <summary>
/// vLLM provider using OpenAI-compatible APIs.
/// </summary>
public sealed class VllmProvider : OpenAiCompatibleProvider
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes provider.
    /// </summary>
    /// <param name="providerId">Provider identifier.</param>
    /// <param name="httpClient">HTTP client.</param>
    /// <param name="defaultModel">Default model.</param>
    /// <param name="apiKey">Optional API key.</param>
    public VllmProvider(string providerId, HttpClient httpClient, string defaultModel, string? apiKey = null)
        : base(providerId, httpClient, defaultModel, apiKey)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc/>
    public override async Task<LlmHealthStatus> CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync("/health", ct);
            bool ok = response.StatusCode == HttpStatusCode.OK;
            return new LlmHealthStatus(ok, ok, ok ? DefaultModel : null, null, null, null);
        }
        catch
        {
            return new LlmHealthStatus(false, false, null, null, null, null);
        }
    }
}
