namespace Codex.Core.Models.Llm;

/// <summary>
/// Represents a non-streaming response from an LLM provider.
/// </summary>
public sealed record LlmResponse
{
    /// <summary>
    /// Gets the returned content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the usage details.
    /// </summary>
    public required LlmUsage Usage { get; init; }

    /// <summary>
    /// Gets the total inference duration.
    /// </summary>
    public required TimeSpan InferenceTime { get; init; }

    /// <summary>
    /// Gets the finish reason, when available.
    /// </summary>
    public string? FinishReason { get; init; }

    /// <summary>
    /// Gets the model identifier used by the provider.
    /// </summary>
    public string? ModelId { get; init; }
}
