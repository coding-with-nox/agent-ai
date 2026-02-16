using System.CommandLine;
using Noxvis.Application.DependencyInjection;
using Noxvis.Cli.Commands;
using Noxvis.Core.Interfaces;
using Noxvis.Infrastructure.DependencyInjection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

ServiceCollection services = new();
services.AddLogging();
services.AddNoxvisApplication();
services.AddNoxvisInfrastructure();
services.AddSingleton(Log.Logger);

ServiceProvider provider = services.BuildServiceProvider();
IMediator mediator = provider.GetRequiredService<IMediator>();
IStackRegistry stackRegistry = provider.GetRequiredService<IStackRegistry>();

RootCommand root = new("NOXVIS CLI Host");
root.AddCommand(StackCommands.Build(mediator, stackRegistry));
root.AddCommand(GenCommands.Build(mediator));

return await root.InvokeAsync(args);
