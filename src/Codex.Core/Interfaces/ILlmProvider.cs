using Codex.Core.Models.Llm;

namespace Codex.Core.Interfaces;

/// <summary>
/// Abstraction for an LLM runtime provider.
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Gets the provider identifier.
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Gets the default model configured for this provider.
    /// </summary>
    string DefaultModel { get; }

    /// <summary>
    /// Sends a non-streaming completion request.
    /// </summary>
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct);

    /// <summary>
    /// Sends a streaming completion request.
    /// </summary>
    IAsyncEnumerable<LlmTokenChunk> StreamAsync(LlmRequest request, CancellationToken ct);

    /// <summary>
    /// Checks provider health.
    /// </summary>
    Task<LlmHealthStatus> CheckHealthAsync(CancellationToken ct);

    /// <summary>
    /// Gets model info for the active model.
    /// </summary>
    Task<LlmModelInfo> GetModelInfoAsync(CancellationToken ct);

    /// <summary>
    /// Ensures a model is available and loaded when supported.
    /// </summary>
    Task<bool> EnsureModelLoadedAsync(string modelId, CancellationToken ct);
}
