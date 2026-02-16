using System.Text.Json.Serialization;

namespace NocodeX.Core.Models;

/// <summary>
/// Represents the active technology stack and conventions.
/// </summary>
public sealed record StackConfig
{
    /// <summary>
    /// Gets the preset name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the primary language.
    /// </summary>
    [JsonPropertyName("language")]
    public required string Language { get; init; }

    /// <summary>
    /// Gets the framework.
    /// </summary>
    [JsonPropertyName("framework")]
    public required string Framework { get; init; }

    /// <summary>
    /// Gets command defaults used by the verification pipeline.
    /// </summary>
    [JsonPropertyName("commands")]
    public required Dictionary<string, string> Commands { get; init; }

    /// <summary>
    /// Gets architecture conventions.
    /// </summary>
    [JsonPropertyName("conventions")]
    public required IReadOnlyList<string> Conventions { get; init; }

    /// <summary>
    /// Gets custom rules for code generation and linting.
    /// </summary>
    [JsonPropertyName("custom_rules")]
    public required IReadOnlyList<string> CustomRules { get; init; }
}
