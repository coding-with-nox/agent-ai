using Codex.Core.Models.Planning;
using Spectre.Console;

namespace Codex.Cli.Rendering;

/// <summary>
/// Renders execution plans in a structured table format.
/// </summary>
public static class PlanRenderer
{
    /// <summary>
    /// Renders an execution plan to the console.
    /// </summary>
    /// <param name="plan">The execution plan to render.</param>
    public static void Render(ExecutionPlan plan)
    {
        AnsiConsole.MarkupLine($"[bold]Plan: {plan.PlanId}[/] — {plan.Description}");
        AnsiConsole.MarkupLine($"[dim]Created: {plan.CreatedAt:yyyy-MM-dd HH:mm:ss}[/]");
        AnsiConsole.MarkupLine($"[dim]Approved: {(plan.IsApproved ? "[green]Yes[/]" : "[yellow]No[/]")}[/]");
        AnsiConsole.WriteLine();

        Table table = new();
        table.Border(TableBorder.Rounded);
        table.AddColumn("Step");
        table.AddColumn("Command");
        table.AddColumn("Depends On");
        table.AddColumn("Status");

        foreach (PlanStep step in plan.Steps)
        {
            string status = step.Status switch
            {
                Core.Enums.TaskStatus.Pending => "[dim]Pending[/]",
                Core.Enums.TaskStatus.Success => "[green]Done[/]",
                Core.Enums.TaskStatus.Failed => $"[red]Failed: {step.Error}[/]",
                Core.Enums.TaskStatus.Blocked => "[yellow]Blocked[/]",
                _ => "[dim]Unknown[/]"
            };

            string deps = step.DependsOn.Count > 0
                ? string.Join(", ", step.DependsOn)
                : "-";

            table.AddRow(step.StepId, step.Command, deps, status);
        }

        AnsiConsole.Write(table);
    }
}
