namespace NocodeX.Core.Models.Llm;

/// <summary>
/// Token usage statistics from an LLM inference call.
/// </summary>
/// <param name="PromptTokens">Number of tokens in the prompt.</param>
/// <param name="CompletionTokens">Number of generated tokens.</param>
/// <param name="TotalTokens">Sum of prompt and completion tokens.</param>
/// <param name="TokensPerSecond">Generation speed metric for local inference.</param>
public sealed record LlmUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    double? TokensPerSecond);
