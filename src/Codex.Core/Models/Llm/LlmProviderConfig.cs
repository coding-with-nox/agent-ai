using Codex.Core.Enums;

namespace Codex.Core.Models.Llm;

/// <summary>
/// Represents static provider configuration.
/// </summary>
public sealed record LlmProviderConfig
{
    /// <summary>
    /// Gets provider identifier.
    /// </summary>
    public required string ProviderId { get; init; }

    /// <summary>
    /// Gets provider type.
    /// </summary>
    public required LlmProviderType Type { get; init; }

    /// <summary>
    /// Gets host name.
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// Gets provider port.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Gets configured default model.
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// Gets optional API key.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Gets optional base path.
    /// </summary>
    public string? BasePath { get; init; }

    /// <summary>
    /// Gets default timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 300;
}
