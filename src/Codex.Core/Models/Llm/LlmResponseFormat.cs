namespace Codex.Core.Models.Llm;

/// <summary>
/// Desired output format from the LLM.
/// </summary>
public enum LlmResponseFormat
{
    /// <summary>Free-form text output.</summary>
    Text,

    /// <summary>JSON-structured output.</summary>
    Json
}
