namespace Codex.Core.Models.Llm;

/// <summary>
/// Describes a loaded model hosted by a provider.
/// </summary>
/// <param name="ModelId">Model identifier.</param>
/// <param name="ContextWindowTokens">Context window size in tokens.</param>
/// <param name="Quantization">Quantization format.</param>
/// <param name="ParameterCount">Parameter count.</param>
/// <param name="VramUsageMb">Estimated VRAM usage in MB.</param>
/// <param name="IsLoaded">Whether the model is currently loaded.</param>
public sealed record LlmModelInfo(
    string ModelId,
    int ContextWindowTokens,
    string Quantization,
    long ParameterCount,
    long VramUsageMb,
    bool IsLoaded);
