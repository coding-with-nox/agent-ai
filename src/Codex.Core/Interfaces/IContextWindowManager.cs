using Codex.Core.Models.Llm;

namespace Codex.Core.Interfaces;

/// <summary>
/// Manages context window constraints for local LLM inference.
/// </summary>
public interface IContextWindowManager
{
    /// <summary>
    /// Estimates the token count for a given text.
    /// </summary>
    /// <param name="text">The text to estimate.</param>
    /// <returns>Estimated token count.</returns>
    int EstimateTokens(string text);

    /// <summary>
    /// Calculates available output tokens after accounting for the prompt.
    /// </summary>
    /// <param name="request">The inference request.</param>
    /// <param name="modelInfo">Current model metadata.</param>
    /// <returns>Number of tokens available for generation.</returns>
    int GetAvailableOutputTokens(LlmRequest request, LlmModelInfo modelInfo);

    /// <summary>
    /// Truncates or summarizes messages to fit within the context window.
    /// </summary>
    /// <param name="messages">Original message list.</param>
    /// <param name="modelInfo">Current model metadata.</param>
    /// <param name="reservedOutputTokens">Tokens reserved for generation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Trimmed message list that fits the context window.</returns>
    Task<IReadOnlyList<LlmMessage>> FitToContextWindowAsync(
        IReadOnlyList<LlmMessage> messages,
        LlmModelInfo modelInfo,
        int reservedOutputTokens,
        CancellationToken ct);
}
