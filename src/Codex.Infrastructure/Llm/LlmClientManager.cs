using Codex.Core.Exceptions;
using Codex.Core.Interfaces;
using Codex.Core.Models.Llm;

namespace Codex.Infrastructure.Llm;

/// <summary>
/// In-memory registry for LLM providers with fallback completion.
/// </summary>
public sealed class LlmClientManager : ILlmClientManager
{
    private readonly Dictionary<string, ILlmProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _fallbackOrder = [];
    private string? _primaryId;

    /// <inheritdoc/>
    public ILlmProvider Primary => _primaryId is not null && _providers.TryGetValue(_primaryId, out ILlmProvider? provider)
        ? provider
        : throw new LlmUnavailableException("No primary LLM provider configured.");

    /// <inheritdoc/>
    public IReadOnlyCollection<ILlmProvider> Providers => _providers.Values.ToArray();

    /// <inheritdoc/>
    public ILlmProvider GetProvider(string providerId)
    {
        if (_providers.TryGetValue(providerId, out ILlmProvider? provider))
        {
            return provider;
        }

        throw new KeyNotFoundException($"LLM provider '{providerId}' was not found.");
    }

    /// <inheritdoc/>
    public void Register(ILlmProvider provider, bool isPrimary = false)
    {
        _providers[provider.ProviderId] = provider;
        if (!_fallbackOrder.Contains(provider.ProviderId, StringComparer.OrdinalIgnoreCase))
        {
            _fallbackOrder.Add(provider.ProviderId);
        }

        if (isPrimary || _primaryId is null)
        {
            _primaryId = provider.ProviderId;
        }
    }

    /// <inheritdoc/>
    public void SetPrimary(string providerId)
    {
        _ = GetProvider(providerId);
        _primaryId = providerId;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, LlmHealthStatus>> CheckAllHealthAsync(CancellationToken ct)
    {
        Dictionary<string, LlmHealthStatus> statuses = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, ILlmProvider> entry in _providers)
        {
            statuses[entry.Key] = await entry.Value.CheckHealthAsync(ct);
        }

        return statuses;
    }

    /// <inheritdoc/>
    public async Task<LlmResponse> CompleteWithFallbackAsync(LlmRequest request, CancellationToken ct)
    {
        if (_providers.Count == 0)
        {
            throw new LlmUnavailableException("No LLM providers are registered.");
        }

        List<string> attempted = [];
        List<ILlmProvider> candidates = BuildCandidates();
        foreach (ILlmProvider provider in candidates)
        {
            attempted.Add(provider.ProviderId);
            try
            {
                LlmHealthStatus health = await provider.CheckHealthAsync(ct);
                if (!health.IsReachable)
                {
                    continue;
                }

                LlmRequest providerRequest = request with { Model = string.IsNullOrWhiteSpace(request.Model) ? provider.DefaultModel : request.Model };
                return await provider.CompleteAsync(providerRequest, ct);
            }
            catch
            {
                // try next fallback provider
            }
        }

        throw new LlmUnavailableException($"All LLM providers failed: {string.Join(", ", attempted)}.");
    }

    private List<ILlmProvider> BuildCandidates()
    {
        List<ILlmProvider> providers = [];
        if (_primaryId is not null && _providers.TryGetValue(_primaryId, out ILlmProvider? primary))
        {
            providers.Add(primary);
        }

        foreach (string fallbackId in _fallbackOrder)
        {
            if (_primaryId is not null && fallbackId.Equals(_primaryId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (_providers.TryGetValue(fallbackId, out ILlmProvider? provider))
            {
                providers.Add(provider);
            }
        }

        return providers;
    }
}
