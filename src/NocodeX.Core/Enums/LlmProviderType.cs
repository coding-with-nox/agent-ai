namespace NocodeX.Core.Enums;

/// <summary>
/// Identifies the type of LLM inference backend.
/// </summary>
public enum LlmProviderType
{
    /// <summary>Ollama local server.</summary>
    Ollama,

    /// <summary>vLLM OpenAI-compatible server.</summary>
    Vllm,

    /// <summary>llama.cpp HTTP server.</summary>
    LlamaCpp,

    /// <summary>Any OpenAI-compatible HTTP server.</summary>
    OpenAiCompatible
}
