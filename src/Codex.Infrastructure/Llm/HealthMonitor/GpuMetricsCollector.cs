using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Codex.Infrastructure.Llm.HealthMonitor;

/// <summary>
/// Collects GPU metrics via nvidia-smi or rocm-smi.
/// </summary>
public sealed class GpuMetricsCollector
{
    private readonly ILogger<GpuMetricsCollector> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GpuMetricsCollector"/> class.
    /// </summary>
    public GpuMetricsCollector(ILogger<GpuMetricsCollector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Collects current GPU metrics from the system.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>GPU metrics or null if no GPU tool is available.</returns>
    public async Task<GpuMetrics?> CollectAsync(CancellationToken ct)
    {
        GpuMetrics? metrics = await TryNvidiaSmiAsync(ct);
        if (metrics is not null) return metrics;

        metrics = await TryRocmSmiAsync(ct);
        return metrics;
    }

    private async Task<GpuMetrics?> TryNvidiaSmiAsync(CancellationToken ct)
    {
        try
        {
            string output = await RunProcessAsync(
                "nvidia-smi",
                "--query-gpu=utilization.gpu,memory.used,memory.free,memory.total,name --format=csv,noheader,nounits",
                ct);

            if (string.IsNullOrWhiteSpace(output)) return null;

            string[] parts = output.Trim().Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 5) return null;

            return new GpuMetrics
            {
                UtilizationPercent = ParseDouble(parts[0]),
                VramUsedMb = ParseLong(parts[1]),
                VramFreeMb = ParseLong(parts[2]),
                VramTotalMb = ParseLong(parts[3]),
                GpuName = parts[4]
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "nvidia-smi not available");
            return null;
        }
    }

    private async Task<GpuMetrics?> TryRocmSmiAsync(CancellationToken ct)
    {
        try
        {
            string output = await RunProcessAsync("rocm-smi", "--showuse --showmemuse --csv", ct);
            if (string.IsNullOrWhiteSpace(output)) return null;

            // rocm-smi CSV output varies, do best-effort parse
            string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return null;

            string[] values = lines[1].Split(',', StringSplitOptions.TrimEntries);

            return new GpuMetrics
            {
                UtilizationPercent = values.Length > 1 ? ParseDouble(values[1]) : 0,
                VramUsedMb = values.Length > 2 ? ParseLong(values[2]) : 0,
                VramFreeMb = 0,
                VramTotalMb = 0,
                GpuName = "AMD GPU"
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "rocm-smi not available");
            return null;
        }
    }

    private static async Task<string> RunProcessAsync(
        string fileName, string arguments, CancellationToken ct)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        string output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return output;
    }

    private static double ParseDouble(string value)
    {
        double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result);
        return result;
    }

    private static long ParseLong(string value)
    {
        long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long result);
        return result;
    }
}

/// <summary>
/// Collected GPU metrics from system tools.
/// </summary>
public sealed class GpuMetrics
{
    /// <summary>Gets or sets the GPU utilization percentage.</summary>
    public double UtilizationPercent { get; set; }

    /// <summary>Gets or sets the VRAM used in MB.</summary>
    public long VramUsedMb { get; set; }

    /// <summary>Gets or sets the VRAM free in MB.</summary>
    public long VramFreeMb { get; set; }

    /// <summary>Gets or sets the total VRAM in MB.</summary>
    public long VramTotalMb { get; set; }

    /// <summary>Gets or sets the GPU device name.</summary>
    public string GpuName { get; set; } = "Unknown";
}
