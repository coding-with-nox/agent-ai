namespace NocodeX.Core.Models.Llm;

/// <summary>
/// A single token chunk emitted during streaming inference.
/// </summary>
/// <param name="Token">The generated token text.</param>
/// <param name="IsComplete">Whether this is the final chunk.</param>
/// <param name="FinalUsage">Usage stats, populated only on the final chunk.</param>
public sealed record LlmTokenChunk(
    string Token,
    bool IsComplete,
    LlmUsage? FinalUsage = null);
