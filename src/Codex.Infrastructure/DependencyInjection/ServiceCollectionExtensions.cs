using System.Text.Json;
using Codex.Core.Enums;
using Codex.Core.Interfaces;
using Codex.Infrastructure.Llm;
using Codex.Infrastructure.Llm.Providers;
using Codex.Infrastructure.Stack;
using Codex.Infrastructure.Stack.Presets;
using Microsoft.Extensions.DependencyInjection;

namespace Codex.Infrastructure.DependencyInjection;

/// <summary>
/// Registers infrastructure-layer services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds infrastructure services.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Updated collection.</returns>
    public static IServiceCollection AddCodexInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IStackPreset, DotnetCleanPreset>();
        services.AddSingleton<IStackPreset, NextjsFullstackPreset>();
        services.AddSingleton<IStackPreset, FastapiHexPreset>();
        services.AddSingleton<IStackPreset, GoMicroPreset>();
        services.AddSingleton<IStackPreset, SpringDddPreset>();
        services.AddSingleton<IStackPreset, RustAxumPreset>();
        services.AddSingleton<IStackPreset, LaravelModularPreset>();
        services.AddSingleton<IStackRegistry, StackRegistry>();
        services.AddSingleton<ILlmClientManager>(_ => BuildLlmClientManager());
        return services;
    }

    private static ILlmClientManager BuildLlmClientManager()
    {
        LlmClientManager manager = new();
        string configPath = Path.Combine(Directory.GetCurrentDirectory(), "codex.config.json");
        if (!File.Exists(configPath))
        {
            return manager;
        }

        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(configPath));
        JsonElement llm = json.RootElement.GetProperty("llm");
        string primaryId = llm.GetProperty("primary_provider").GetString() ?? string.Empty;

        foreach (JsonElement providerNode in llm.GetProperty("providers").EnumerateArray())
        {
            string providerId = providerNode.GetProperty("provider_id").GetString() ?? throw new InvalidOperationException("provider_id missing");
            string type = providerNode.GetProperty("type").GetString() ?? throw new InvalidOperationException("type missing");
            string host = providerNode.GetProperty("host").GetString() ?? "localhost";
            int port = providerNode.GetProperty("port").GetInt32();
            string model = providerNode.TryGetProperty("model", out JsonElement modelNode) ? modelNode.GetString() ?? "" : "";
            string basePath = providerNode.TryGetProperty("base_path", out JsonElement basePathNode) ? basePathNode.GetString() ?? string.Empty : string.Empty;
            string endpoint = string.IsNullOrWhiteSpace(basePath)
                ? $"http://{host}:{port}"
                : $"http://{host}:{port}{basePath.TrimEnd('/')}";

            HttpClient client = new() { BaseAddress = new Uri(endpoint) };
            ILlmProvider provider = ParseType(type) switch
            {
                LlmProviderType.Ollama => new OllamaProvider(providerId, client, model),
                LlmProviderType.Vllm => new VllmProvider(providerId, client, model),
                LlmProviderType.LlamaCpp => new LlamaCppProvider(providerId, client, model),
                _ => new OpenAiCompatibleProvider(providerId, client, model)
            };

            manager.Register(provider, providerId.Equals(primaryId, StringComparison.OrdinalIgnoreCase));
        }

        return manager;
    }

    private static LlmProviderType ParseType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "ollama" => LlmProviderType.Ollama,
            "vllm" => LlmProviderType.Vllm,
            "llama-cpp" => LlmProviderType.LlamaCpp,
            _ => LlmProviderType.OpenAiCompatible
        };
    }
}
