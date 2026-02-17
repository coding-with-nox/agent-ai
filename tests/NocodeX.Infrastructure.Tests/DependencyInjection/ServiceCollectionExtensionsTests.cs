using NocodeX.Core.Models;
using NocodeX.Core.Models.Llm;
using NocodeX.Infrastructure.Acp;
using NocodeX.Infrastructure.Configuration;
using NocodeX.Infrastructure.DependencyInjection;
using NocodeX.Infrastructure.Mcp;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace NocodeX.Infrastructure.Tests.DependencyInjection;

/// <summary>
/// Tests for infrastructure dependency registration from runtime configuration.
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddNocodeXLlm_WithEnabledMcpAndAcp_RegistersConfiguredClients()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddNocodeXInfrastructure();
        services.AddNocodeXLlm(CreateConfig(
            mcpEnabled: true,
            acpEnabled: true));

        using ServiceProvider provider = services.BuildServiceProvider();

        McpClientManager mcpManager = provider.GetRequiredService<McpClientManager>();
        AcpClientManager acpManager = provider.GetRequiredService<AcpClientManager>();

        mcpManager.GetServerIds().Should().Contain("mcp-context7");
        acpManager.GetAgentIds().Should().Contain("acp-reviewer");
    }

    [Fact]
    public void AddNocodeXLlm_WithDisabledMcpAndAcp_DoesNotRegisterConfiguredClients()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddNocodeXInfrastructure();
        services.AddNocodeXLlm(CreateConfig(
            mcpEnabled: false,
            acpEnabled: false));

        using ServiceProvider provider = services.BuildServiceProvider();

        McpClientManager mcpManager = provider.GetRequiredService<McpClientManager>();
        AcpClientManager acpManager = provider.GetRequiredService<AcpClientManager>();

        mcpManager.GetServerIds().Should().NotContain("mcp-context7");
        acpManager.GetAgentIds().Should().NotContain("acp-reviewer");
    }

    private static NocodeXConfiguration CreateConfig(bool mcpEnabled, bool acpEnabled)
    {
        return new NocodeXConfiguration
        {
            Llm = new LlmConfiguration
            {
                PrimaryProvider = "ollama-local",
                Providers =
                [
                    new()
                    {
                        ProviderId = "ollama-local",
                        Type = NocodeX.Core.Enums.LlmProviderType.Ollama,
                        Host = "localhost",
                        Port = 11434,
                        Model = "qwen2.5-coder:7b",
                        TimeoutSeconds = 30
                    }
                ],
                FallbackChain = ["ollama-local"]
            },
            McpServers =
            [
                new("mcp-context7", "stdio", "npx -y @upstash/context7-mcp", mcpEnabled)
            ],
            AcpAgents =
            [
                new("acp-reviewer", "internal://llm", NocodeX.Core.Enums.TrustLevel.ReviewRequired, acpEnabled)
            ]
        };
    }
}
