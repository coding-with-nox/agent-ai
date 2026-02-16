using NocodeX.Core.Models;
using NocodeX.Core.Models.Llm;
using NocodeX.Core.Models.Prompts;

namespace NocodeX.Core.Interfaces;

/// <summary>
/// Builds LLM prompts adapted to the active model and stack.
/// </summary>
public interface IPromptBuilder
{
    /// <summary>
    /// Constructs a prompt request adapted to the model's capabilities.
    /// </summary>
    /// <param name="context">Prompt context with task details.</param>
    /// <param name="modelInfo">Active model metadata.</param>
    /// <param name="stack">Active stack configuration.</param>
    /// <returns>A fully formed LLM request.</returns>
    LlmRequest BuildPrompt(
        PromptContext context,
        LlmModelInfo modelInfo,
        StackConfig stack);
}
