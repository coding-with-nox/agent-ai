namespace Codex.Core.Enums;

/// <summary>
/// Supported LLM provider runtime types.
/// </summary>
public enum LlmProviderType
{
    /// <summary>
    /// Ollama provider.
    /// </summary>
    Ollama,

    /// <summary>
    /// vLLM provider.
    /// </summary>
    Vllm,

    /// <summary>
    /// llama.cpp provider.
    /// </summary>
    LlamaCpp,

    /// <summary>
    /// Generic OpenAI-compatible provider.
    /// </summary>
    OpenAiCompatible
}
