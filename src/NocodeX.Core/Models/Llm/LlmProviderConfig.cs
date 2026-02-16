using NocodeX.Core.Enums;

namespace NocodeX.Core.Models.Llm;

/// <summary>
/// Configuration for a single LLM inference provider.
/// </summary>
public sealed record LlmProviderConfig
{
    /// <summary>Gets the unique provider identifier.</summary>
    public required string ProviderId { get; init; }

    /// <summary>Gets the provider backend type.</summary>
    public required LlmProviderType Type { get; init; }

    /// <summary>Gets the server hostname or IP address.</summary>
    public required string Host { get; init; }

    /// <summary>Gets the server port number.</summary>
    public required int Port { get; init; }

    /// <summary>Gets the model identifier to use.</summary>
    public string? Model { get; init; }

    /// <summary>Gets the optional base path prefix for the API.</summary>
    public string? BasePath { get; init; }

    /// <summary>Gets the environment variable name holding the API key.</summary>
    public string? ApiKeyEnv { get; init; }

    /// <summary>Gets an optional override for the context window size.</summary>
    public int? ContextWindowOverride { get; init; }

    /// <summary>Gets the default sampling temperature.</summary>
    public float DefaultTemperature { get; init; } = 0.2f;

    /// <summary>Gets the default maximum tokens for generation.</summary>
    public int DefaultMaxTokens { get; init; } = 8192;

    /// <summary>Gets the per-request timeout in seconds.</summary>
    public int TimeoutSeconds { get; init; } = 300;

    /// <summary>Gets whether to auto-pull missing models (Ollama only).</summary>
    public bool AutoPull { get; init; }

    /// <summary>Gets the number of GPU layers to offload (-1 for all).</summary>
    public int GpuLayers { get; init; } = -1;

    /// <summary>Gets the base URL computed from host and port.</summary>
    public string BaseUrl => $"http://{Host}:{Port}{BasePath ?? string.Empty}";
}
