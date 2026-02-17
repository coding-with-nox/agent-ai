using NocodeX.Core.Enums;
using NocodeX.Core.Interfaces;
using NocodeX.Core.Models;
using NocodeX.Core.Models.Llm;
using NocodeX.Infrastructure.Acp;
using NocodeX.Infrastructure.AuditLog;
using NocodeX.Infrastructure.CodeGeneration;
using NocodeX.Infrastructure.Configuration;
using NocodeX.Infrastructure.Llm;
using NocodeX.Infrastructure.Llm.HealthMonitor;
using NocodeX.Infrastructure.Llm.Prompts;
using NocodeX.Infrastructure.Llm.Providers;
using NocodeX.Infrastructure.Mcp;
using NocodeX.Infrastructure.Planning;
using NocodeX.Infrastructure.SelfCorrection;
using NocodeX.Infrastructure.Stack;
using NocodeX.Infrastructure.Stack.Presets;
using NocodeX.Infrastructure.Verification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NocodeX.Infrastructure.DependencyInjection;

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
    public static IServiceCollection AddNocodeXInfrastructure(this IServiceCollection services)
    {
        // Stack presets
        services.AddSingleton<IStackPreset, DotnetCleanPreset>();
        services.AddSingleton<IStackPreset, NextjsFullstackPreset>();
        services.AddSingleton<IStackPreset, FastapiHexPreset>();
        services.AddSingleton<IStackPreset, GoMicroPreset>();
        services.AddSingleton<IStackPreset, SpringDddPreset>();
        services.AddSingleton<IStackPreset, RustAxumPreset>();
        services.AddSingleton<IStackPreset, LaravelModularPreset>();
        services.AddSingleton<IStackRegistry, StackRegistry>();

        // HTTP client factory
        services.AddHttpClient();

        // Planning
        services.AddSingleton<CommandChainParser>();
        services.AddSingleton<IPlanEngine, DagPlanEngine>();

        // Verification
        services.AddSingleton<IVerificationService, VerificationService>();

        // Audit log
        services.AddSingleton<IAuditLog>(sp =>
        {
            string logPath = Path.Combine(
                Directory.GetCurrentDirectory(), ".nocodex", "audit.jsonl");
            return new FileAuditLog(logPath, sp.GetRequiredService<ILogger<FileAuditLog>>());
        });

        // Code generation
        services.AddSingleton<CodeBlockParser>();
        services.AddSingleton(sp =>
        {
            string workspace = Directory.GetCurrentDirectory();
            return new FileOutputMapper(workspace, sp.GetRequiredService<ILogger<FileOutputMapper>>());
        });

        // Token estimation and context management
        services.AddSingleton<TokenEstimator>();
        services.AddSingleton<IContextWindowManager, ContextWindowManager>();

        // GPU metrics
        services.AddSingleton<GpuMetricsCollector>();

        // MCP
        services.AddSingleton<McpClientManager>();

        // ACP
        services.AddSingleton<AcpClientManager>();

        return services;
    }

    /// <summary>
    /// Adds LLM provider services from configuration.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="config">NOcodeX configuration.</param>
    /// <returns>Updated collection.</returns>
    public static IServiceCollection AddNocodeXLlm(
        this IServiceCollection services,
        NocodeXConfiguration config)
    {
        // Register providers
        foreach (LlmProviderConfig providerConfig in config.Llm.Providers)
        {
            services.AddSingleton<ILlmProvider>(sp =>
            {
                IHttpClientFactory httpFactory = sp.GetRequiredService<IHttpClientFactory>();
                ILoggerFactory logFactory = sp.GetRequiredService<ILoggerFactory>();

                return providerConfig.Type switch
                {
                    LlmProviderType.Ollama => new OllamaProvider(
                        providerConfig, httpFactory,
                        logFactory.CreateLogger<OllamaProvider>()),
                    LlmProviderType.Vllm => new VllmProvider(
                        providerConfig, httpFactory,
                        logFactory.CreateLogger<VllmProvider>()),
                    LlmProviderType.LlamaCpp => new LlamaCppProvider(
                        providerConfig, httpFactory,
                        logFactory.CreateLogger<LlamaCppProvider>()),
                    _ => new OpenAiCompatibleProvider(
                        providerConfig, httpFactory,
                        logFactory.CreateLogger<OpenAiCompatibleProvider>())
                };
            });
        }

        // LLM Client Manager
        services.AddSingleton<ILlmClientManager>(sp =>
        {
            IEnumerable<ILlmProvider> providers = sp.GetServices<ILlmProvider>();
            ILogger<LlmClientManager> logger = sp.GetRequiredService<ILogger<LlmClientManager>>();

            LlmClientManager manager = new(logger, providers, config.Llm.FallbackChain);

            // Set primary
            if (!string.IsNullOrEmpty(config.Llm.PrimaryProvider))
            {
                try
                {
                    ILlmProvider primary = manager.GetProvider(config.Llm.PrimaryProvider);
                    manager.Register(primary, isPrimary: true);
                }
                catch (KeyNotFoundException)
                {
                    logger.LogWarning("Primary provider '{Id}' not found", config.Llm.PrimaryProvider);
                }
            }

            return manager;
        });

        // Prompt builder
        services.AddSingleton(sp =>
        {
            string templatePath = Path.Combine(
                AppContext.BaseDirectory, "Llm", "Prompts", "Templates");
            return new PromptTemplateStore(
                templatePath, sp.GetRequiredService<ILogger<PromptTemplateStore>>());
        });
        services.AddSingleton<IPromptBuilder, PromptBuilder>();

        // LLM Health Monitor
        services.AddSingleton<LlmHealthMonitorService>();

        // Self-correction engine
        services.AddSingleton<SelfCorrectionEngine>();

        // Code generator
        services.AddSingleton<LlmCodeGenerator>();

        // Request router
        services.AddSingleton(sp =>
        {
            ILlmClientManager clientManager = sp.GetRequiredService<ILlmClientManager>();
            ILogger<LlmRequestRouter> logger = sp.GetRequiredService<ILogger<LlmRequestRouter>>();
            return new LlmRequestRouter(clientManager, config.Llm.RoutingRules, logger);
        });

        // MCP clients from configuration (stdio transport only in current implementation).
        foreach (McpServerConfig server in config.McpServers.Where(s => s.Enabled))
        {
            if (!string.Equals(server.Transport, "stdio", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            services.AddSingleton<IMcpClient>(sp =>
            {
                ILogger<McpStdioClient> logger = sp.GetRequiredService<ILogger<McpStdioClient>>();
                return new McpStdioClient(server, logger);
            });
        }

        // ACP agents from configuration.
        foreach (AcpAgentConfig agent in config.AcpAgents.Where(a => a.Enabled))
        {
            services.AddSingleton<IAcpClient>(sp =>
            {
                ILlmClientManager llmClientManager = sp.GetRequiredService<ILlmClientManager>();
                ILogger<AcpAgentClient> logger = sp.GetRequiredService<ILogger<AcpAgentClient>>();
                return new AcpAgentClient(agent, llmClientManager, logger);
            });
        }

        return services;
    }
}
