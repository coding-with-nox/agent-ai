using System.CommandLine;
using Codex.Application.Llm;
using MediatR;

namespace Codex.Cli.Commands;

/// <summary>
/// Builds LLM management commands.
/// </summary>
public static class LlmCommands
{
    /// <summary>
    /// Creates /llm command tree.
    /// </summary>
    /// <param name="mediator">Mediator for command dispatch.</param>
    /// <returns>Configured command tree.</returns>
    public static Command Build(IMediator mediator)
    {
        Command llm = new("llm", "Manage self-hosted LLM providers.");

        Command status = new("status", "Show provider health status.");
        status.SetHandler(async () =>
        {
            IReadOnlyDictionary<string, Codex.Core.Models.Llm.LlmHealthStatus> statuses = await mediator.Send(new LlmStatusQuery());
            foreach ((string provider, Codex.Core.Models.Llm.LlmHealthStatus health) in statuses)
            {
                string indicator = health.IsReachable ? "✓" : "✗";
                Console.WriteLine($"{provider}: {indicator} reachable={health.IsReachable} modelLoaded={health.IsModelLoaded} activeModel={health.ActiveModel ?? "-"}");
            }

            if (statuses.Count == 0)
            {
                Console.WriteLine("No providers registered. Check codex.config.json llm.providers.");
            }
        });

        Command health = new("health", "Alias for status.");
        health.SetHandler(async () => await status.InvokeAsync(""));

        llm.AddCommand(status);
        llm.AddCommand(health);
        return llm;
    }
}
