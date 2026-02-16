using NocodeX.Core.Models.Llm;

namespace NocodeX.Core.Interfaces;

/// <summary>
/// Abstraction for an LLM inference backend.
/// </summary>
public interface ILlmProvider
{
    /// <summary>Gets the unique provider identifier.</summary>
    string ProviderId { get; }

    /// <summary>
    /// Sends a chat completion request and returns the full response.
    /// </summary>
    /// <param name="request">The inference request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The completed LLM response.</returns>
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct);

    /// <summary>
    /// Streams tokens as they are generated.
    /// </summary>
    /// <param name="request">The inference request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async stream of token chunks.</returns>
    IAsyncEnumerable<LlmTokenChunk> StreamAsync(
        LlmRequest request, CancellationToken ct);

    /// <summary>
    /// Checks if the backend is reachable and a model is loaded.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The provider health status.</returns>
    Task<LlmHealthStatus> CheckHealthAsync(CancellationToken ct);

    /// <summary>
    /// Gets information about the currently loaded model.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Model metadata.</returns>
    Task<LlmModelInfo> GetModelInfoAsync(CancellationToken ct);

    /// <summary>
    /// Pulls or loads a model if supported by the runtime.
    /// </summary>
    /// <param name="modelId">The model to ensure is loaded.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the model is loaded and ready.</returns>
    Task<bool> EnsureModelLoadedAsync(string modelId, CancellationToken ct);
}
