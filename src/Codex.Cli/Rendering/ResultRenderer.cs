using Codex.Core.Models;
using Spectre.Console;

namespace Codex.Cli.Rendering;

/// <summary>
/// Renders task results with formatting and color.
/// </summary>
public static class ResultRenderer
{
    /// <summary>
    /// Renders a task result to the console.
    /// </summary>
    /// <param name="result">The task result to render.</param>
    public static void Render(TaskResult result)
    {
        string icon = result.Status == Core.Enums.TaskStatus.Success ? "[green]OK[/]" : "[red]FAIL[/]";

        AnsiConsole.MarkupLine($"{icon} {result.Message}");

        if (result.OutputFiles.Count > 0)
        {
            AnsiConsole.MarkupLine("[dim]Output files:[/]");
            foreach (string file in result.OutputFiles)
            {
                AnsiConsole.MarkupLine($"  [blue]{file}[/]");
            }
        }
    }
}
