namespace NocodeX.Core.Models.Llm;

/// <summary>
/// Contains the result of a non-streaming LLM inference call.
/// </summary>
public sealed record LlmResponse
{
    /// <summary>Gets the generated text content.</summary>
    public required string Content { get; init; }

    /// <summary>Gets token usage statistics.</summary>
    public required LlmUsage Usage { get; init; }

    /// <summary>Gets the wall-clock time spent on inference.</summary>
    public required TimeSpan InferenceTime { get; init; }

    /// <summary>Gets the reason generation stopped (stop, length, etc.).</summary>
    public string? FinishReason { get; init; }

    /// <summary>Gets the model identifier that served the request.</summary>
    public string? ModelId { get; init; }
}
