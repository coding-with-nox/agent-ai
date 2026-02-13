namespace Codex.Core.Models.Llm;

/// <summary>
/// Represents an inference request sent to an LLM provider.
/// </summary>
public sealed record LlmRequest
{
    /// <summary>
    /// Gets the target model.
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// Gets the chat messages for the request.
    /// </summary>
    public required IReadOnlyList<LlmMessage> Messages { get; init; }

    /// <summary>
    /// Gets the sampling temperature.
    /// </summary>
    public float Temperature { get; init; } = 0.2f;

    /// <summary>
    /// Gets the max output token count.
    /// </summary>
    public int MaxTokens { get; init; } = 8192;

    /// <summary>
    /// Gets the nucleus sampling top-p.
    /// </summary>
    public float TopP { get; init; } = 0.95f;

    /// <summary>
    /// Gets the optional repetition penalty.
    /// </summary>
    public float? RepetitionPenalty { get; init; }

    /// <summary>
    /// Gets stop sequences.
    /// </summary>
    public IReadOnlyList<string>? StopSequences { get; init; }

    /// <summary>
    /// Gets response format preference.
    /// </summary>
    public LlmResponseFormat? ResponseFormat { get; init; }

    /// <summary>
    /// Gets optional provider timeout override.
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}
