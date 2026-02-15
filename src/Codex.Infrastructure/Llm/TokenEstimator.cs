using Codex.Core.Models.Llm;

namespace Codex.Infrastructure.Llm;

/// <summary>
/// Provides heuristic token-count estimation for text and chat messages.
/// </summary>
/// <remarks>
/// <para>
/// This estimator uses a simple character-to-token ratio (<c>text.Length / 4</c>) as
/// the default heuristic. This ratio is a widely adopted approximation for English text
/// across GPT-class tokenizers (cl100k_base, o200k_base) where the average token length
/// is roughly four characters.
/// </para>
/// <para>
/// For precise token counting, integrate a dedicated tokenizer library (e.g. tiktoken).
/// This estimator is intended for fast, allocation-free budget checks where exact counts
/// are not required.
/// </para>
/// </remarks>
public sealed class TokenEstimator
{
    /// <summary>
    /// The average number of characters per token used in the heuristic.
    /// </summary>
    private const int CharsPerToken = 4;

    /// <summary>
    /// The per-message overhead in tokens that accounts for role tags, separator tokens,
    /// and other formatting that the model's message encoding adds around each message.
    /// </summary>
    /// <remarks>
    /// OpenAI's cl100k_base encoding uses approximately 4 tokens of overhead per message:
    /// <c>&lt;|start|&gt;{role}&lt;|sep|&gt;</c> (3 tokens) plus a trailing separator (1 token).
    /// </remarks>
    private const int TokensPerMessageOverhead = 4;

    /// <summary>
    /// Estimates the number of tokens in a plain text string using a character-based heuristic.
    /// </summary>
    /// <param name="text">
    /// The text to estimate tokens for. If <c>null</c> or empty, zero is returned.
    /// </param>
    /// <returns>
    /// A non-negative estimated token count. The estimate is computed as
    /// <c>ceiling(text.Length / 4)</c>, with a minimum of <c>1</c> for non-empty input.
    /// </returns>
    /// <example>
    /// <code>
    /// var estimator = new TokenEstimator();
    /// int tokens = estimator.EstimateTokens("Hello, world!");
    /// // tokens == 4  (13 chars / 4, rounded up)
    /// </code>
    /// </example>
    public int EstimateTokens(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        int estimate = (text.Length + CharsPerToken - 1) / CharsPerToken;

        // Any non-empty string produces at least one token.
        return Math.Max(estimate, 1);
    }

    /// <summary>
    /// Estimates the total token count for a list of chat messages, including per-message
    /// formatting overhead.
    /// </summary>
    /// <param name="messages">
    /// The messages to estimate tokens for. Must not be <c>null</c>.
    /// </param>
    /// <returns>
    /// A non-negative estimated total token count. Each message contributes:
    /// <list type="bullet">
    ///   <item><description>
    ///     <see cref="TokensPerMessageOverhead"/> tokens for role and framing.
    ///   </description></item>
    ///   <item><description>
    ///     The heuristic token estimate for the <see cref="LlmMessage.Role"/> field.
    ///   </description></item>
    ///   <item><description>
    ///     The heuristic token estimate for the <see cref="LlmMessage.Content"/> field.
    ///   </description></item>
    /// </list>
    /// An additional <c>3</c> tokens are added at the end to account for the reply priming
    /// overhead (<c>&lt;|start|&gt;assistant&lt;|sep|&gt;</c>).
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="messages"/> is <c>null</c>.
    /// </exception>
    /// <example>
    /// <code>
    /// var estimator = new TokenEstimator();
    /// var messages = new List&lt;LlmMessage&gt;
    /// {
    ///     new("system", "You are a helpful assistant."),
    ///     new("user", "Hello!")
    /// };
    /// int total = estimator.EstimateTokensForMessages(messages);
    /// </code>
    /// </example>
    public int EstimateTokensForMessages(IReadOnlyList<LlmMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        int total = 0;

        for (int i = 0; i < messages.Count; i++)
        {
            LlmMessage message = messages[i];

            // Overhead for role/separator tokens surrounding each message.
            total += TokensPerMessageOverhead;

            // Tokens for the role string (e.g. "system", "user", "assistant").
            total += EstimateTokens(message.Role);

            // Tokens for the message content body.
            total += EstimateTokens(message.Content);
        }

        // Reply priming: the model's response is kicked off with
        // <|start|>assistant<|sep|> which costs approximately 3 tokens.
        if (messages.Count > 0)
        {
            total += 3;
        }

        return total;
    }
}
