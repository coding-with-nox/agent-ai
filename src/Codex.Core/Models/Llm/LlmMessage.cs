namespace Codex.Core.Models.Llm;

/// <summary>
/// Represents a single message in a chat completion conversation.
/// </summary>
/// <param name="Role">Message role (system, user, assistant).</param>
/// <param name="Content">Message text content.</param>
public sealed record LlmMessage(string Role, string Content);
