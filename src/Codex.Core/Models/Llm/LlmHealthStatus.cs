namespace Codex.Core.Models.Llm;

/// <summary>
/// Represents provider health and runtime telemetry.
/// </summary>
/// <param name="IsReachable">Whether the provider is reachable over network.</param>
/// <param name="IsModelLoaded">Whether at least one model is loaded and ready.</param>
/// <param name="ActiveModel">Active model identifier.</param>
/// <param name="GpuUtilizationPercent">GPU utilization percent if known.</param>
/// <param name="VramFreeMb">Free VRAM in MB if known.</param>
/// <param name="AverageTokensPerSecond">Average throughput in tokens/sec.</param>
public sealed record LlmHealthStatus(
    bool IsReachable,
    bool IsModelLoaded,
    string? ActiveModel,
    double? GpuUtilizationPercent,
    long? VramFreeMb,
    double? AverageTokensPerSecond);
