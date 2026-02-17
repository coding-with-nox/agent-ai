using NocodeX.Core.Interfaces;
using NocodeX.Core.Models.Llm;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NocodeX.Infrastructure.Llm.HealthMonitor;

/// <summary>
/// Background service that periodically polls all LLM provider health status.
/// </summary>
public sealed class LlmHealthMonitorService : BackgroundService
{
    private readonly ILlmClientManager _clientManager;
    private readonly GpuMetricsCollector _gpuCollector;
    private readonly ILogger<LlmHealthMonitorService> _logger;
    private readonly TimeSpan _interval;

    private readonly Dictionary<string, LlmHealthStatus> _latestStatus = [];
    private readonly object _statusLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmHealthMonitorService"/> class.
    /// </summary>
    public LlmHealthMonitorService(
        ILlmClientManager clientManager,
        GpuMetricsCollector gpuCollector,
        ILogger<LlmHealthMonitorService> logger,
        TimeSpan? interval = null)
    {
        _clientManager = clientManager;
        _gpuCollector = gpuCollector;
        _logger = logger;
        _interval = interval ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Gets the latest health status for all providers.
    /// </summary>
    /// <returns>Health status keyed by provider ID.</returns>
    public IReadOnlyDictionary<string, LlmHealthStatus> GetLatestStatus()
    {
        lock (_statusLock)
        {
            return new Dictionary<string, LlmHealthStatus>(_latestStatus);
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LLM health monitor started, polling every {Interval}s", _interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollHealthAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health poll");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("LLM health monitor stopped");
    }

    private async Task PollHealthAsync(CancellationToken ct)
    {
        IReadOnlyDictionary<string, LlmHealthStatus> statuses =
            await _clientManager.CheckAllHealthAsync(ct);

        GpuMetrics? gpuMetrics = await _gpuCollector.CollectAsync(ct);

        lock (_statusLock)
        {
            _latestStatus.Clear();
            foreach (KeyValuePair<string, LlmHealthStatus> kvp in statuses)
            {
                LlmHealthStatus enriched = gpuMetrics is not null
                    ? kvp.Value with
                    {
                        GpuUtilizationPercent = gpuMetrics.UtilizationPercent,
                        VramFreeMb = gpuMetrics.VramFreeMb
                    }
                    : kvp.Value;

                _latestStatus[kvp.Key] = enriched;
            }
        }

        int healthy = statuses.Values.Count(s => s.IsReachable && s.IsModelLoaded);
        _logger.LogDebug("Health poll: {Healthy}/{Total} providers healthy",
            healthy, statuses.Count);
    }
}
