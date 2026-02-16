namespace NocodeX.Core.Models.Llm;

/// <summary>
/// Metadata about a model loaded on an LLM inference backend.
/// </summary>
/// <param name="ModelId">Model identifier or name.</param>
/// <param name="ContextWindowTokens">Maximum context window in tokens.</param>
/// <param name="Quantization">Quantization method (e.g. Q4_K_M, fp16, AWQ).</param>
/// <param name="ParameterCount">Total model parameters (e.g. 7_000_000_000).</param>
/// <param name="VramUsageMb">Current VRAM consumption in megabytes.</param>
/// <param name="IsLoaded">Whether the model is currently loaded and ready.</param>
public sealed record LlmModelInfo(
    string ModelId,
    int ContextWindowTokens,
    string Quantization,
    long ParameterCount,
    long VramUsageMb,
    bool IsLoaded);
