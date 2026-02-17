using System.Text.Json;
using System.Text.Json.Serialization;
using NocodeX.Core.Enums;
using NocodeX.Core.Models;
using NocodeX.Core.Models.Llm;
using Microsoft.Extensions.Logging;

namespace NocodeX.Infrastructure.Configuration;

/// <summary>
/// Root configuration model for the NOcodeX agent.
/// </summary>
public sealed class NocodeXConfiguration
{
    /// <summary>Gets or sets the environment name.</summary>
    [JsonPropertyName("environment")]
    public string Environment { get; set; } = "development";

    /// <summary>Gets or sets the agent version.</summary>
    [JsonPropertyName("agent_version")]
    public string AgentVersion { get; set; } = "2.0.0";

    /// <summary>Gets or sets the workspace root path.</summary>
    [JsonPropertyName("workspace_root")]
    public string WorkspaceRoot { get; set; } = ".";

    /// <summary>Gets or sets the GitHub workflow configuration.</summary>
    [JsonPropertyName("github")]
    public GitHubConfiguration GitHub { get; set; } = new();

    /// <summary>Gets or sets the label definitions for issue management.</summary>
    [JsonPropertyName("labels")]
    public LabelsConfiguration Labels { get; set; } = new();

    /// <summary>Gets or sets the LLM configuration section.</summary>
    [JsonPropertyName("llm")]
    public LlmConfiguration Llm { get; set; } = new();

    /// <summary>Gets or sets the MCP server configurations.</summary>
    [JsonPropertyName("mcp_servers")]
    public List<McpServerConfig> McpServers { get; set; } = new();

    /// <summary>Gets or sets the ACP agent configurations.</summary>
    [JsonPropertyName("acp_agents")]
    public List<AcpAgentConfig> AcpAgents { get; set; } = new();

    /// <summary>Gets or sets the execution limits.</summary>
    [JsonPropertyName("limits")]
    public LimitsConfiguration Limits { get; set; } = new();

    /// <summary>
    /// Loads configuration from a JSON file.
    /// </summary>
    /// <param name="filePath">Path to nocodex.config.json.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>Parsed configuration.</returns>
    public static NocodeXConfiguration Load(string filePath, ILogger? logger = null)
    {
        if (!File.Exists(filePath))
        {
            logger?.LogWarning("Config file not found at {Path}, using defaults", filePath);
            return new NocodeXConfiguration();
        }

        string json = File.ReadAllText(filePath);
        NocodeXConfiguration? config = JsonSerializer.Deserialize<NocodeXConfiguration>(json, JsonOpts);
        return config ?? new NocodeXConfiguration();
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

/// <summary>
/// GitHub workflow and repository configuration.
/// </summary>
public sealed class GitHubConfiguration
{
    /// <summary>Gets or sets the target repository in owner/repo format.</summary>
    [JsonPropertyName("target_repo")]
    public string TargetRepo { get; set; } = string.Empty;

    /// <summary>Gets or sets whether to auto-create the repo if it does not exist.</summary>
    [JsonPropertyName("auto_create")]
    public bool AutoCreate { get; set; }

    /// <summary>Gets or sets the default branch name.</summary>
    [JsonPropertyName("default_branch")]
    public string DefaultBranch { get; set; } = "main";

    /// <summary>Gets or sets the repository visibility (private/public).</summary>
    [JsonPropertyName("visibility")]
    public string Visibility { get; set; } = "private";

    /// <summary>Gets or sets the local workspace directory for cloning target repos.</summary>
    [JsonPropertyName("workspace_directory")]
    public string WorkspaceDirectory { get; set; } = "./workspaces";

    /// <summary>Gets or sets the test command to run in the target repo.</summary>
    [JsonPropertyName("test_command")]
    public string TestCommand { get; set; } = "dotnet test *.sln -c Release";

    /// <summary>Gets or sets the commit message conventions.</summary>
    [JsonPropertyName("commit_conventions")]
    public CommitConventionsConfiguration CommitConventions { get; set; } = new();

    /// <summary>Gets or sets the PR template settings.</summary>
    [JsonPropertyName("pr_template")]
    public PrTemplateConfiguration PrTemplate { get; set; } = new();
}

/// <summary>
/// Commit message conventions configuration.
/// </summary>
public sealed class CommitConventionsConfiguration
{
    /// <summary>Gets or sets the allowed commit prefix pattern.</summary>
    [JsonPropertyName("prefix_pattern")]
    public string PrefixPattern { get; set; } = "feat|fix|chore|refactor|docs|test";

    /// <summary>Gets or sets the commit message template.</summary>
    [JsonPropertyName("message_template")]
    public string MessageTemplate { get; set; } = "{prefix}: resolve #{issue_number} {title}";

    /// <summary>Gets or sets the max subject line length.</summary>
    [JsonPropertyName("max_subject_length")]
    public int MaxSubjectLength { get; set; } = 72;
}

/// <summary>
/// Pull request template configuration.
/// </summary>
public sealed class PrTemplateConfiguration
{
    /// <summary>Gets or sets the PR title template.</summary>
    [JsonPropertyName("title_template")]
    public string TitleTemplate { get; set; } = "Resolve #{issue_number} - {title}";

    /// <summary>Gets or sets the PR body template.</summary>
    [JsonPropertyName("body_template")]
    public string BodyTemplate { get; set; } = "Modifiche automatiche per #{issue_number}\n\nCloses #{issue_number}";
}

/// <summary>
/// Label definitions for issue state and priority management.
/// </summary>
public sealed class LabelsConfiguration
{
    /// <summary>Gets or sets the workflow state labels.</summary>
    [JsonPropertyName("states")]
    public List<LabelDefinition> States { get; set; } = new();

    /// <summary>Gets or sets the priority labels.</summary>
    [JsonPropertyName("priorities")]
    public List<LabelDefinition> Priorities { get; set; } = new();

    /// <summary>Gets or sets the default priority label name.</summary>
    [JsonPropertyName("default_priority")]
    public string DefaultPriority { get; set; } = "p3";
}

/// <summary>
/// A single label definition with name, color, and description.
/// </summary>
public sealed class LabelDefinition
{
    /// <summary>Gets or sets the label name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the label hex color (without #).</summary>
    [JsonPropertyName("color")]
    public string Color { get; set; } = "EDEDED";

    /// <summary>Gets or sets the label description.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// LLM-specific configuration section.
/// </summary>
public sealed class LlmConfiguration
{
    /// <summary>Gets or sets the primary provider identifier.</summary>
    [JsonPropertyName("primary_provider")]
    public string PrimaryProvider { get; set; } = "ollama-local";

    /// <summary>Gets or sets provider configurations.</summary>
    [JsonPropertyName("providers")]
    public List<LlmProviderJsonConfig> Providers { get; set; } = new();

    /// <summary>Gets or sets the fallback chain of provider IDs.</summary>
    [JsonPropertyName("fallback_chain")]
    public List<string> FallbackChain { get; set; } = new();

    /// <summary>Gets or sets routing rules.</summary>
    [JsonPropertyName("routing_rules")]
    public List<LlmRoutingRule> RoutingRules { get; set; } = new();
}

/// <summary>
/// JSON-serializable LLM provider configuration.
/// </summary>
public sealed class LlmProviderJsonConfig
{
    /// <summary>Gets or sets the provider identifier.</summary>
    [JsonPropertyName("provider_id")]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>Gets or sets the provider type string.</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "ollama";

    /// <summary>Gets or sets the server host.</summary>
    [JsonPropertyName("host")]
    public string Host { get; set; } = "localhost";

    /// <summary>Gets or sets the server port.</summary>
    [JsonPropertyName("port")]
    public int Port { get; set; } = 11434;

    /// <summary>Gets or sets the model identifier.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>Gets or sets the base path for the API.</summary>
    [JsonPropertyName("base_path")]
    public string? BasePath { get; set; }

    /// <summary>Gets or sets an optional absolute base URL override for the API endpoint.</summary>
    [JsonPropertyName("base_url")]
    public string? BaseUrl { get; set; }

    /// <summary>Gets or sets the API key environment variable name.</summary>
    [JsonPropertyName("api_key_env")]
    public string? ApiKeyEnv { get; set; }

    /// <summary>Gets or sets the context window override.</summary>
    [JsonPropertyName("context_window_override")]
    public int? ContextWindowOverride { get; set; }

    /// <summary>Gets or sets the default temperature.</summary>
    [JsonPropertyName("default_temperature")]
    public float DefaultTemperature { get; set; } = 0.2f;

    /// <summary>Gets or sets the default max tokens.</summary>
    [JsonPropertyName("default_max_tokens")]
    public int DefaultMaxTokens { get; set; } = 8192;

    /// <summary>Gets or sets the timeout in seconds.</summary>
    [JsonPropertyName("timeout_seconds")]
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>Gets or sets whether to auto-pull models.</summary>
    [JsonPropertyName("auto_pull")]
    public bool AutoPull { get; set; }

    /// <summary>
    /// Converts to a strongly-typed provider config.
    /// </summary>
    public LlmProviderConfig ToProviderConfig()
    {
        LlmProviderType providerType = Type.ToLowerInvariant() switch
        {
            "ollama" => LlmProviderType.Ollama,
            "vllm" => LlmProviderType.Vllm,
            "llama-cpp" or "llamacpp" => LlmProviderType.LlamaCpp,
            _ => LlmProviderType.OpenAiCompatible
        };

        return new LlmProviderConfig
        {
            ProviderId = ProviderId,
            Type = providerType,
            Host = Host,
            Port = Port,
            Model = Model,
            BasePath = BasePath,
            BaseUrlOverride = BaseUrl,
            ApiKeyEnv = ApiKeyEnv,
            ContextWindowOverride = ContextWindowOverride,
            DefaultTemperature = DefaultTemperature,
            DefaultMaxTokens = DefaultMaxTokens,
            TimeoutSeconds = TimeoutSeconds,
            AutoPull = AutoPull
        };
    }
}

/// <summary>
/// Execution limits configuration.
/// </summary>
public sealed class LimitsConfiguration
{
    /// <summary>Gets or sets the max tokens per task.</summary>
    [JsonPropertyName("max_tokens_per_task")]
    public int MaxTokensPerTask { get; set; } = 100_000;

    /// <summary>Gets or sets the max API calls per task.</summary>
    [JsonPropertyName("max_api_calls_per_task")]
    public int MaxApiCallsPerTask { get; set; } = 50;

    /// <summary>Gets or sets the max execution time in minutes.</summary>
    [JsonPropertyName("max_execution_time_minutes")]
    public int MaxExecutionTimeMinutes { get; set; } = 15;

    /// <summary>Gets or sets the max file size in lines.</summary>
    [JsonPropertyName("max_file_size_lines")]
    public int MaxFileSizeLines { get; set; } = 300;

    /// <summary>Gets or sets the max concurrent LLM requests.</summary>
    [JsonPropertyName("max_concurrent_llm_requests")]
    public int MaxConcurrentLlmRequests { get; set; } = 1;

    /// <summary>Gets or sets the max self-correction attempts.</summary>
    [JsonPropertyName("max_self_correction_attempts")]
    public int MaxSelfCorrectionAttempts { get; set; } = 3;
}
