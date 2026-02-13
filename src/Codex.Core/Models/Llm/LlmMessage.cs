namespace Codex.Core.Models.Llm;

/// <summary>
/// Represents a chat message sent to an LLM.
/// </summary>
/// <param name="Role">Message role (system, user, assistant).</param>
/// <param name="Content">Message content.</param>
public sealed record LlmMessage(string Role, string Content);
