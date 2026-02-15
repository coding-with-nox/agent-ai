using System.CommandLine;
using Codex.Application.DependencyInjection;
using Codex.Cli.Commands;
using Codex.Cli.Rendering;
using Codex.Core.Interfaces;
using Codex.Infrastructure.Acp;
using Codex.Infrastructure.Configuration;
using Codex.Infrastructure.DependencyInjection;
using Codex.Infrastructure.Mcp;
using Codex.Infrastructure.Planning;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        Path.Combine(Directory.GetCurrentDirectory(), ".codex", "codex.log"),
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

// Load configuration
string configPath = Path.Combine(Directory.GetCurrentDirectory(), "codex.config.json");
CodexConfiguration config = CodexConfiguration.Load(configPath);

// Build DI container
ServiceCollection services = new();
services.AddLogging(builder =>
{
    builder.ClearProviders();
    builder.AddSerilog(Log.Logger);
});

services.AddCodexApplication();
services.AddCodexInfrastructure();
services.AddCodexLlm(config);

ServiceProvider provider = services.BuildServiceProvider();

// Resolve services
IMediator mediator = provider.GetRequiredService<IMediator>();
IStackRegistry stackRegistry = provider.GetRequiredService<IStackRegistry>();
ILlmClientManager llmClientManager = provider.GetRequiredService<ILlmClientManager>();
McpClientManager mcpManager = provider.GetRequiredService<McpClientManager>();
AcpClientManager acpManager = provider.GetRequiredService<AcpClientManager>();
CommandChainParser chainParser = provider.GetRequiredService<CommandChainParser>();
IPlanEngine planEngine = provider.GetRequiredService<IPlanEngine>();

// Build CLI
RootCommand root = new("CODEX CLI Host — Self-Hosted LLM Edition");
root.AddCommand(StackCommands.Build(mediator, stackRegistry));
root.AddCommand(GenCommands.Build(mediator));
root.AddCommand(LlmCommands.Build(mediator, llmClientManager));
root.AddCommand(McpCommands.Build(mcpManager));
root.AddCommand(AcpCommands.Build(acpManager));
root.AddCommand(PipelineCommands.BuildPlanCommand(chainParser, planEngine));
root.AddCommand(PipelineCommands.BuildApproveCommand(planEngine));

// Show banner on bare invocation
root.SetHandler(() =>
{
    InitStatus status = new()
    {
        Stack = stackRegistry.Current?.Name,
        LlmStatus = BuildLlmStatusLine(llmClientManager),
        McpStatus = $"{mcpManager.GetServerIds().Count} server(s) registered",
        AcpStatus = $"{acpManager.GetAgentIds().Count} agent(s) registered",
        WorkspacePath = Path.GetFullPath(config.WorkspaceRoot),
        LimitsInfo = $"{config.Limits.MaxTokensPerTask / 1000}K tokens | " +
                     $"{config.Limits.MaxApiCallsPerTask} calls | " +
                     $"{config.Limits.MaxExecutionTimeMinutes}min"
    };

    StatusRenderer.RenderBanner(status);
});

return await root.InvokeAsync(args);

static string BuildLlmStatusLine(ILlmClientManager manager)
{
    try
    {
        IReadOnlyList<string> ids = manager.GetProviderIds();
        return ids.Count > 0
            ? $"{ids.Count} provider(s): {string.Join(", ", ids)}"
            : "No providers configured";
    }
    catch
    {
        return "No providers configured";
    }
}
