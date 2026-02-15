namespace Codex.Core.Models.Llm;

/// <summary>
/// Health check result for an LLM inference provider.
/// </summary>
/// <param name="IsReachable">Whether the server is responding.</param>
/// <param name="IsModelLoaded">Whether a model is loaded and ready.</param>
/// <param name="ActiveModel">The currently active model identifier.</param>
/// <param name="GpuUtilizationPercent">Current GPU utilization percentage.</param>
/// <param name="VramFreeMb">Available VRAM in megabytes.</param>
/// <param name="AverageTokensPerSecond">Recent average generation speed.</param>
public sealed record LlmHealthStatus(
    bool IsReachable,
    bool IsModelLoaded,
    string? ActiveModel,
    double? GpuUtilizationPercent,
    long? VramFreeMb,
    double? AverageTokensPerSecond);
