using System.CommandLine;
using System.Text.Json;
using Noxvis.Application.Stack;
using Noxvis.Core.Interfaces;
using MediatR;

namespace Noxvis.Cli.Commands;

/// <summary>
/// Builds stack commands.
/// </summary>
public static class StackCommands
{
    /// <summary>
    /// Creates /stack command tree.
    /// </summary>
    /// <param name="mediator">Mediator for command dispatch.</param>
    /// <param name="stackRegistry">Stack registry.</param>
    /// <returns>Configured command.</returns>
    public static Command Build(IMediator mediator, IStackRegistry stackRegistry)
    {
        Command stack = new("stack", "Manage active stack configuration.");

        Command set = new("set", "Set the active stack preset.");
        Argument<string> presetArg = new("preset", "Preset name.");
        set.AddArgument(presetArg);
        set.SetHandler(async (string preset) =>
        {
            await mediator.Send(new SetStackCommand(preset));
            Console.WriteLine($"Active stack set to '{preset}'.");
        }, presetArg);

        Command show = new("show", "Show current stack JSON.");
        show.SetHandler(async () =>
        {
            Noxvis.Core.Models.StackConfig? config = await mediator.Send(new ShowStackQuery());
            if (config is null)
            {
                Console.WriteLine("No stack configured.");
                return;
            }

            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
        });

        Command presets = new("presets", "List all built-in stack presets.");
        presets.SetHandler(() =>
        {
            foreach (string name in stackRegistry.Presets())
            {
                Console.WriteLine(name);
            }
        });

        Command validate = new("validate", "Validate the current stack configuration.");
        validate.SetHandler(() =>
        {
            if (stackRegistry.Current is null)
            {
                Console.WriteLine("No stack configured.");
                return;
            }

            IReadOnlyList<string> errors = stackRegistry.Validate(stackRegistry.Current);
            if (errors.Count == 0)
            {
                Console.WriteLine("Stack configuration is valid.");
                return;
            }

            foreach (string error in errors)
            {
                Console.WriteLine($"- {error}");
            }
        });

        stack.AddCommand(set);
        stack.AddCommand(show);
        stack.AddCommand(presets);
        stack.AddCommand(validate);
        return stack;
    }
}
