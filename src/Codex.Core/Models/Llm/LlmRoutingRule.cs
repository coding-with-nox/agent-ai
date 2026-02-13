namespace Codex.Core.Models.Llm;

/// <summary>
/// Represents a static provider routing rule.
/// </summary>
/// <param name="Condition">Rule condition expression.</param>
/// <param name="Provider">Target provider id.</param>
/// <param name="Reason">Human readable routing reason.</param>
public sealed record LlmRoutingRule(string Condition, string Provider, string Reason);
