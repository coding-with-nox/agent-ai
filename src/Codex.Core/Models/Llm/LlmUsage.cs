namespace Codex.Core.Models.Llm;

/// <summary>
/// Represents token usage statistics for an LLM call.
/// </summary>
/// <param name="PromptTokens">Prompt tokens.</param>
/// <param name="CompletionTokens">Completion tokens.</param>
/// <param name="TotalTokens">Total tokens.</param>
/// <param name="TokensPerSecond">Measured generation throughput.</param>
public sealed record LlmUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    double? TokensPerSecond);
