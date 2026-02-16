using NocodeX.Core.Interfaces;
using NocodeX.Core.Models.Llm;
using NocodeX.Core.Models.Prompts;
using Microsoft.Extensions.Logging;

namespace NocodeX.Infrastructure.Llm;

/// <summary>
/// Routes LLM requests to providers based on configurable routing rules that
/// evaluate prompt type and task complexity.
/// </summary>
public sealed class LlmRequestRouter
{
    private static readonly StringComparer OrdinalIgnoreCase = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Ordered complexity levels used for comparison operators in rule conditions.
    /// </summary>
    private static readonly IReadOnlyList<string> ComplexityLevels = new[]
    {
        "trivial", "low", "medium", "high", "critical"
    };

    private readonly ILlmClientManager _clientManager;
    private readonly IReadOnlyList<LlmRoutingRule> _rules;
    private readonly ILogger<LlmRequestRouter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmRequestRouter"/> class.
    /// </summary>
    /// <param name="clientManager">The client manager that owns provider registrations.</param>
    /// <param name="rules">Ordered routing rules to evaluate.</param>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    public LlmRequestRouter(
        ILlmClientManager clientManager,
        IReadOnlyList<LlmRoutingRule> rules,
        ILogger<LlmRequestRouter> logger)
    {
        _clientManager = clientManager ?? throw new ArgumentNullException(nameof(clientManager));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Evaluates routing rules against the given prompt type and task complexity,
    /// returning the identifier of the best-matching registered provider.
    /// </summary>
    /// <param name="promptType">The type of prompt being issued.</param>
    /// <param name="taskComplexity">A complexity label (e.g. "low", "medium", "high").</param>
    /// <returns>
    /// The provider identifier from the first matching rule whose provider is registered,
    /// or the primary provider's identifier if no rule matches.
    /// </returns>
    public string ResolveProvider(PromptType promptType, string taskComplexity)
    {
        var registeredIds = _clientManager.GetProviderIds();

        foreach (var rule in _rules)
        {
            if (EvaluateCondition(rule.Condition, promptType, taskComplexity))
            {
                if (registeredIds.Contains(rule.Provider))
                {
                    _logger.LogDebug(
                        "Routing rule matched: '{Condition}' -> provider '{Provider}' ({Reason})",
                        rule.Condition,
                        rule.Provider,
                        rule.Reason);
                    return rule.Provider;
                }

                _logger.LogWarning(
                    "Routing rule matched '{Condition}' but provider '{Provider}' is not registered, skipping",
                    rule.Condition,
                    rule.Provider);
            }
        }

        var primaryId = _clientManager.Primary.ProviderId;
        _logger.LogDebug(
            "No routing rule matched for PromptType={PromptType}, Complexity={Complexity}; using primary '{ProviderId}'",
            promptType,
            taskComplexity,
            primaryId);
        return primaryId;
    }

    /// <summary>
    /// Resolves the routed provider and returns the <see cref="ILlmProvider"/> instance.
    /// </summary>
    /// <param name="promptType">The type of prompt being issued.</param>
    /// <param name="taskComplexity">A complexity label (e.g. "low", "medium", "high").</param>
    /// <returns>The provider instance selected by the routing rules.</returns>
    public ILlmProvider GetRoutedProvider(PromptType promptType, string taskComplexity)
    {
        var providerId = ResolveProvider(promptType, taskComplexity);
        return _clientManager.GetProvider(providerId);
    }

    /// <summary>
    /// Evaluates a simple condition expression against the current prompt type and complexity.
    /// Supported forms:
    /// <list type="bullet">
    ///   <item><c>prompt_type == 'Explain'</c></item>
    ///   <item><c>task_complexity &gt;= 'high'</c></item>
    ///   <item><c>task_complexity == 'low'</c></item>
    /// </list>
    /// </summary>
    private static bool EvaluateCondition(string condition, PromptType promptType, string taskComplexity)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return false;

        // Normalise the condition for easier parsing.
        var trimmed = condition.Trim();

        if (trimmed.Contains("prompt_type", StringComparison.OrdinalIgnoreCase))
        {
            var value = ExtractValue(trimmed);
            if (value is null) return false;

            if (trimmed.Contains("=="))
                return string.Equals(promptType.ToString(), value, StringComparison.OrdinalIgnoreCase);
            if (trimmed.Contains("!="))
                return !string.Equals(promptType.ToString(), value, StringComparison.OrdinalIgnoreCase);
        }

        if (trimmed.Contains("task_complexity", StringComparison.OrdinalIgnoreCase))
        {
            var value = ExtractValue(trimmed);
            if (value is null) return false;

            int actual = ComplexityRank(taskComplexity);
            int target = ComplexityRank(value);

            if (trimmed.Contains(">=")) return actual >= target;
            if (trimmed.Contains("<=")) return actual <= target;
            if (trimmed.Contains("!=")) return actual != target;
            if (trimmed.Contains("==")) return actual == target;
            if (trimmed.Contains('>') && !trimmed.Contains(">=")) return actual > target;
            if (trimmed.Contains('<') && !trimmed.Contains("<=")) return actual < target;
        }

        return false;
    }

    /// <summary>
    /// Extracts the quoted value from a condition expression (e.g. "'high'" returns "high").
    /// </summary>
    private static string? ExtractValue(string expression)
    {
        var firstQuote = expression.IndexOf('\'');
        if (firstQuote < 0) return null;

        var secondQuote = expression.IndexOf('\'', firstQuote + 1);
        if (secondQuote < 0) return null;

        return expression[(firstQuote + 1)..secondQuote];
    }

    /// <summary>
    /// Maps a complexity label to a numeric rank for comparison.
    /// Unrecognised labels receive rank 0.
    /// </summary>
    private static int ComplexityRank(string complexity)
    {
        for (int i = 0; i < ComplexityLevels.Count; i++)
        {
            if (string.Equals(ComplexityLevels[i], complexity, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return 0;
    }
}
