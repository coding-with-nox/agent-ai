namespace Codex.Core.Models.Llm;

/// <summary>
/// Encapsulates a chat completion request to an LLM provider.
/// </summary>
public sealed record LlmRequest
{
    /// <summary>Gets the model identifier to use for inference.</summary>
    public required string Model { get; init; }

    /// <summary>Gets the ordered list of conversation messages.</summary>
    public required IReadOnlyList<LlmMessage> Messages { get; init; }

    /// <summary>Gets the sampling temperature (0.0–2.0).</summary>
    public float Temperature { get; init; } = 0.2f;

    /// <summary>Gets the maximum tokens to generate.</summary>
    public int MaxTokens { get; init; } = 8192;

    /// <summary>Gets the nucleus sampling probability threshold.</summary>
    public float TopP { get; init; } = 0.95f;

    /// <summary>Gets the optional repetition penalty factor.</summary>
    public float? RepetitionPenalty { get; init; }

    /// <summary>Gets optional stop sequences that terminate generation.</summary>
    public IReadOnlyList<string>? StopSequences { get; init; }

    /// <summary>Gets the desired response format.</summary>
    public LlmResponseFormat? ResponseFormat { get; init; }

    /// <summary>Gets the optional per-request timeout.</summary>
    public TimeSpan? Timeout { get; init; }
}
