using System.CommandLine;
using Noxvis.Application.Generation;
using Noxvis.Core.Exceptions;
using MediatR;

namespace Noxvis.Cli.Commands;

/// <summary>
/// Builds generation commands.
/// </summary>
public static class GenCommands
{
    /// <summary>
    /// Creates /gen:endpoint command.
    /// </summary>
    /// <param name="mediator">Mediator for command dispatch.</param>
    /// <returns>Configured command.</returns>
    public static Command Build(IMediator mediator)
    {
        Command endpoint = new("gen:endpoint", "Generate an endpoint scaffold.");
        Argument<string> methodArg = new("method", "HTTP method.");
        Argument<string> routeArg = new("route", "Route path.");
        endpoint.AddArgument(methodArg);
        endpoint.AddArgument(routeArg);

        endpoint.SetHandler(async (string method, string route) =>
        {
            try
            {
                Noxvis.Core.Models.TaskResult result = await mediator.Send(new GenEndpointCommand(method, route));
                Console.WriteLine(result.Message);
            }
            catch (NoStackConfiguredException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }, methodArg, routeArg);

        return endpoint;
    }
}
