using Codex.Core.Models.Llm;

namespace Codex.Core.Interfaces;

/// <summary>
/// Manages provider registration, health and fallback completion.
/// </summary>
public interface ILlmClientManager
{
    /// <summary>
    /// Gets the primary provider.
    /// </summary>
    ILlmProvider Primary { get; }

    /// <summary>
    /// Gets all registered providers.
    /// </summary>
    IReadOnlyCollection<ILlmProvider> Providers { get; }

    /// <summary>
    /// Gets provider by id.
    /// </summary>
    ILlmProvider GetProvider(string providerId);

    /// <summary>
    /// Registers provider and optionally sets it as primary.
    /// </summary>
    void Register(ILlmProvider provider, bool isPrimary = false);

    /// <summary>
    /// Sets primary provider by id.
    /// </summary>
    void SetPrimary(string providerId);

    /// <summary>
    /// Checks health for all providers.
    /// </summary>
    Task<IReadOnlyDictionary<string, LlmHealthStatus>> CheckAllHealthAsync(CancellationToken ct);

    /// <summary>
    /// Completes request with fallback chain.
    /// </summary>
    Task<LlmResponse> CompleteWithFallbackAsync(LlmRequest request, CancellationToken ct);
}
