using NocodeX.Core.Interfaces;
using NocodeX.Core.Models.Llm;
using Microsoft.Extensions.Logging;

namespace NocodeX.Infrastructure.Llm;

/// <summary>
/// Manages context window constraints for local LLM inference.
/// </summary>
public sealed class ContextWindowManager : IContextWindowManager
{
    private readonly TokenEstimator _tokenEstimator;
    private readonly ILogger<ContextWindowManager> _logger;
    private const int SafetyMarginTokens = 256;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextWindowManager"/> class.
    /// </summary>
    public ContextWindowManager(
        TokenEstimator tokenEstimator,
        ILogger<ContextWindowManager> logger)
    {
        _tokenEstimator = tokenEstimator;
        _logger = logger;
    }

    /// <inheritdoc/>
    public int EstimateTokens(string text)
    {
        return _tokenEstimator.EstimateTokens(text);
    }

    /// <inheritdoc/>
    public int GetAvailableOutputTokens(LlmRequest request, LlmModelInfo modelInfo)
    {
        int promptTokens = 0;
        foreach (LlmMessage msg in request.Messages)
        {
            promptTokens += EstimateTokens(msg.Content) + 4;
        }

        int available = modelInfo.ContextWindowTokens - promptTokens - SafetyMarginTokens;
        _logger.LogDebug(
            "Context budget: {Window} total, {Prompt} prompt, {Available} available",
            modelInfo.ContextWindowTokens, promptTokens, available);

        return Math.Max(0, available);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<LlmMessage>> FitToContextWindowAsync(
        IReadOnlyList<LlmMessage> messages,
        LlmModelInfo modelInfo,
        int reservedOutputTokens,
        CancellationToken ct)
    {
        int budget = modelInfo.ContextWindowTokens - reservedOutputTokens - SafetyMarginTokens;
        if (budget <= 0)
        {
            _logger.LogWarning("Context budget exhausted: {Window} - {Reserved} - {Margin} <= 0",
                modelInfo.ContextWindowTokens, reservedOutputTokens, SafetyMarginTokens);
            return Task.FromResult<IReadOnlyList<LlmMessage>>(Array.Empty<LlmMessage>());
        }

        List<LlmMessage> result = [];
        int usedTokens = 0;

        // Always keep system prompt if present
        if (messages.Count > 0 && messages[0].Role == "system")
        {
            int systemTokens = EstimateTokens(messages[0].Content) + 4;
            if (systemTokens <= budget)
            {
                result.Add(messages[0]);
                usedTokens += systemTokens;
            }
        }

        // Add messages from newest to oldest (priority: recent context)
        List<LlmMessage> nonSystem = [];
        for (int i = (messages.Count > 0 && messages[0].Role == "system") ? 1 : 0; i < messages.Count; i++)
        {
            nonSystem.Add(messages[i]);
        }

        List<LlmMessage> kept = [];
        for (int i = nonSystem.Count - 1; i >= 0; i--)
        {
            ct.ThrowIfCancellationRequested();
            int msgTokens = EstimateTokens(nonSystem[i].Content) + 4;
            if (usedTokens + msgTokens <= budget)
            {
                kept.Insert(0, nonSystem[i]);
                usedTokens += msgTokens;
            }
            else
            {
                int dropped = i + 1;
                _logger.LogInformation("Dropped {Count} older messages to fit context window", dropped);
                break;
            }
        }

        result.AddRange(kept);
        _logger.LogDebug("Fitted {Count} messages using {Tokens}/{Budget} tokens",
            result.Count, usedTokens, budget);
        return Task.FromResult<IReadOnlyList<LlmMessage>>(result);
    }
}
