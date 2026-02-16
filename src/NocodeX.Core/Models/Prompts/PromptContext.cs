namespace NocodeX.Core.Models.Prompts;

/// <summary>
/// Provides contextual data for building an LLM prompt.
/// </summary>
public sealed record PromptContext
{
    /// <summary>Gets the human-readable task description.</summary>
    public required string TaskDescription { get; init; }

    /// <summary>Gets the type of prompt to generate.</summary>
    public required PromptType Type { get; init; }

    /// <summary>Gets existing source files keyed by relative path.</summary>
    public IReadOnlyDictionary<string, string>? ExistingFiles { get; init; }

    /// <summary>Gets error output for self-correction flows.</summary>
    public string? ErrorContext { get; init; }

    /// <summary>Gets the code from a previous failed attempt.</summary>
    public string? PreviousAttempt { get; init; }
}
