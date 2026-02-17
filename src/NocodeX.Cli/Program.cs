using System.CommandLine;
using NocodeX.Application.DependencyInjection;
using NocodeX.Cli.Commands;
using NocodeX.Cli.Rendering;
using NocodeX.Core.Interfaces;
using NocodeX.Infrastructure.Acp;
using NocodeX.Infrastructure.Configuration;
using NocodeX.Infrastructure.DependencyInjection;
using NocodeX.Infrastructure.Mcp;
using NocodeX.Infrastructure.Planning;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Formatting.Compact;

// Configure Serilog — structured JSON for machine consumption + plain text for humans
string logDir = Path.Combine(Directory.GetCurrentDirectory(), ".nocodex");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "NocodeX.Cli")
    .WriteTo.File(
        Path.Combine(logDir, "nocodex.log"),
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        new CompactJsonFormatter(),
        Path.Combine(logDir, "nocodex.json.log"),
        rollingInterval: RollingInterval.Day)
    .CreateLogger();

// Load configuration
using ILoggerFactory bootstrapLoggerFactory = LoggerFactory.Create(builder =>
{
    builder.ClearProviders();
    builder.AddSerilog(Log.Logger);
});

Microsoft.Extensions.Logging.ILogger bootstrapLogger = bootstrapLoggerFactory.CreateLogger("Bootstrap");
string configPath = Path.Combine(Directory.GetCurrentDirectory(), "nocodex.config.json");
NocodeXConfiguration config = NocodeXConfiguration.Load(configPath, bootstrapLogger);

// Build DI container
ServiceCollection services = new();
services.AddLogging(builder =>
{
    builder.ClearProviders();
    builder.AddSerilog(Log.Logger);
});

services.AddNocodeXApplication();
services.AddNocodeXInfrastructure();
services.AddNocodeXLlm(config);

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
RootCommand root = new("NOcodeX CLI Host — Self-Hosted LLM Edition");
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
        TargetRepo = string.IsNullOrWhiteSpace(config.GitHub.TargetRepo) ? "(non configurato)" : config.GitHub.TargetRepo,
        WorkspaceDirectory = config.GitHub.WorkspaceDirectory,
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
