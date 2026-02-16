using NocodeX.Core.Models.Llm;

namespace NocodeX.Core.Interfaces;

/// <summary>
/// Manages multiple LLM providers with fallback and routing capabilities.
/// </summary>
public interface ILlmClientManager
{
    /// <summary>Gets the primary active provider.</summary>
    ILlmProvider Primary { get; }

    /// <summary>
    /// Gets a specific provider by its identifier.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <returns>The requested provider.</returns>
    ILlmProvider GetProvider(string providerId);

    /// <summary>
    /// Registers a new provider at runtime.
    /// </summary>
    /// <param name="provider">The provider instance.</param>
    /// <param name="isPrimary">Whether to set as the primary provider.</param>
    void Register(ILlmProvider provider, bool isPrimary = false);

    /// <summary>
    /// Runs health checks on all registered providers.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Health status keyed by provider identifier.</returns>
    Task<IReadOnlyDictionary<string, LlmHealthStatus>> CheckAllHealthAsync(
        CancellationToken ct);

    /// <summary>
    /// Completes a request with automatic failover across the provider chain.
    /// </summary>
    /// <param name="request">The inference request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The first successful response.</returns>
    Task<LlmResponse> CompleteWithFallbackAsync(
        LlmRequest request, CancellationToken ct);

    /// <summary>
    /// Gets all registered provider identifiers.
    /// </summary>
    /// <returns>Provider identifiers.</returns>
    IReadOnlyList<string> GetProviderIds();
}
