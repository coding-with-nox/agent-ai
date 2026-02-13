using System.CommandLine;
using Codex.Application.DependencyInjection;
using Codex.Cli.Commands;
using Codex.Core.Interfaces;
using Codex.Infrastructure.DependencyInjection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

ServiceCollection services = new();
services.AddLogging();
services.AddCodexApplication();
services.AddCodexInfrastructure();
services.AddSingleton(Log.Logger);

ServiceProvider provider = services.BuildServiceProvider();
IMediator mediator = provider.GetRequiredService<IMediator>();
IStackRegistry stackRegistry = provider.GetRequiredService<IStackRegistry>();

RootCommand root = new("CODEX CLI Host");
root.AddCommand(StackCommands.Build(mediator, stackRegistry));
root.AddCommand(GenCommands.Build(mediator));

return await root.InvokeAsync(args);
