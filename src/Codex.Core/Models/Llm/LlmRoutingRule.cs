namespace Codex.Core.Models.Llm;

/// <summary>
/// Defines a rule for routing LLM requests to specific providers.
/// </summary>
/// <param name="Condition">Rule condition expression (e.g. task_complexity >= 'high').</param>
/// <param name="Provider">Target provider identifier.</param>
/// <param name="Reason">Human-readable explanation for the routing rule.</param>
public sealed record LlmRoutingRule(
    string Condition,
    string Provider,
    string Reason);
