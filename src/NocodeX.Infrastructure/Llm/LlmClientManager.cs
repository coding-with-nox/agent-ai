using System.Collections.Concurrent;
using NocodeX.Core.Exceptions;
using NocodeX.Core.Interfaces;
using NocodeX.Core.Models.Llm;
using Microsoft.Extensions.Logging;

namespace NocodeX.Infrastructure.Llm;

/// <summary>
/// Manages multiple <see cref="ILlmProvider"/> instances with primary selection,
/// registration, health checking, and automatic failover capabilities.
/// </summary>
public sealed class LlmClientManager : ILlmClientManager
{
    private readonly ConcurrentDictionary<string, ILlmProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyList<string> _fallbackChain;
    private readonly ILogger<LlmClientManager> _logger;

    private volatile ILlmProvider? _primary;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmClientManager"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    /// <param name="providers">Providers injected from the DI container.</param>
    /// <param name="fallbackChain">Ordered list of provider identifiers used during failover.</param>
    public LlmClientManager(
        ILogger<LlmClientManager> logger,
        IEnumerable<ILlmProvider> providers,
        IReadOnlyList<string> fallbackChain)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fallbackChain = fallbackChain ?? throw new ArgumentNullException(nameof(fallbackChain));

        ArgumentNullException.ThrowIfNull(providers);

        foreach (var provider in providers)
        {
            _providers.TryAdd(provider.ProviderId, provider);
        }

        // Set the first provider in the fallback chain as the default primary if it exists.
        if (_fallbackChain.Count > 0 &&
            _providers.TryGetValue(_fallbackChain[0], out var firstFallback))
        {
            _primary = firstFallback;
        }
    }

    /// <inheritdoc />
    /// <exception cref="LlmUnavailableException">Thrown when no primary provider has been set.</exception>
    public ILlmProvider Primary =>
        _primary ?? throw new LlmUnavailableException("No primary LLM provider has been configured.");

    /// <inheritdoc />
    /// <exception cref="KeyNotFoundException">Thrown when the provider identifier is not registered.</exception>
    public ILlmProvider GetProvider(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);

        if (_providers.TryGetValue(providerId, out var provider))
        {
            return provider;
        }

        throw new KeyNotFoundException(
            $"LLM provider '{providerId}' is not registered. Available: {string.Join(", ", _providers.Keys)}");
    }

    /// <inheritdoc />
    public void Register(ILlmProvider provider, bool isPrimary = false)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _providers[provider.ProviderId] = provider;
        _logger.LogInformation("Registered LLM provider '{ProviderId}'", provider.ProviderId);

        if (isPrimary)
        {
            _primary = provider;
            _logger.LogInformation("Set primary LLM provider to '{ProviderId}'", provider.ProviderId);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, LlmHealthStatus>> CheckAllHealthAsync(CancellationToken ct)
    {
        var results = new ConcurrentDictionary<string, LlmHealthStatus>(StringComparer.OrdinalIgnoreCase);

        var tasks = _providers.Select(async kvp =>
        {
            try
            {
                var status = await kvp.Value.CheckHealthAsync(ct);
                results[kvp.Key] = status;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Health check failed for provider '{ProviderId}'", kvp.Key);
                results[kvp.Key] = new LlmHealthStatus(
                    IsReachable: false,
                    IsModelLoaded: false,
                    ActiveModel: null,
                    GpuUtilizationPercent: null,
                    VramFreeMb: null,
                    AverageTokensPerSecond: null);
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }

    /// <inheritdoc />
    /// <exception cref="LlmUnavailableException">
    /// Thrown when the primary provider and all fallback providers fail to produce a response.
    /// </exception>
    public async Task<LlmResponse> CompleteWithFallbackAsync(LlmRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Build the ordered attempt list: primary first, then fallback chain entries.
        var attemptOrder = new List<string>();

        if (_primary is not null)
        {
            attemptOrder.Add(_primary.ProviderId);
        }

        foreach (var id in _fallbackChain)
        {
            if (!attemptOrder.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                attemptOrder.Add(id);
            }
        }

        if (attemptOrder.Count == 0)
        {
            throw new LlmUnavailableException("No LLM providers are configured for fallback completion.");
        }

        Exception? lastException = null;

        foreach (var providerId in attemptOrder)
        {
            if (!_providers.TryGetValue(providerId, out var provider))
            {
                _logger.LogWarning("Fallback provider '{ProviderId}' is not registered, skipping", providerId);
                continue;
            }

            try
            {
                _logger.LogDebug("Attempting completion with provider '{ProviderId}'", providerId);
                var response = await provider.CompleteAsync(request, ct);

                if (!string.IsNullOrEmpty(response.Content))
                {
                    _logger.LogDebug(
                        "Provider '{ProviderId}' returned successful response ({Tokens} tokens)",
                        providerId,
                        response.Usage.TotalTokens);
                    return response;
                }

                _logger.LogWarning(
                    "Provider '{ProviderId}' returned an empty response, trying next fallback",
                    providerId);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(
                    ex,
                    "Provider '{ProviderId}' failed during completion, trying next fallback",
                    providerId);
            }
        }

        throw new LlmUnavailableException(
            "All LLM providers failed to produce a response. Check '/llm status' for details.",
            lastException!);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetProviderIds()
    {
        return _providers.Keys.ToList().AsReadOnly();
    }
}
