namespace Codex.Core.Models.Llm;

/// <summary>
/// Represents a streaming token chunk from an LLM provider.
/// </summary>
/// <param name="Token">Generated token fragment.</param>
/// <param name="IsComplete">Whether this chunk marks completion.</param>
/// <param name="FinalUsage">Final usage metrics if available on completion.</param>
public sealed record LlmTokenChunk(
    string Token,
    bool IsComplete,
    LlmUsage? FinalUsage = null);
