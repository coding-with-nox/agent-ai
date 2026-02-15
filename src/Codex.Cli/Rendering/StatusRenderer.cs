using Spectre.Console;

namespace Codex.Cli.Rendering;

/// <summary>
/// Renders the CODEX agent initialization status banner.
/// </summary>
public static class StatusRenderer
{
    /// <summary>
    /// Renders the startup banner with system status.
    /// </summary>
    /// <param name="status">Initialization status data.</param>
    public static void RenderBanner(InitStatus status)
    {
        Rule rule = new("[bold]CODEX Agent v2.0 · Self-Hosted LLM Edition · Initialized[/]");
        rule.Style = Style.Parse("blue");
        AnsiConsole.Write(rule);

        Table table = new();
        table.Border(TableBorder.None);
        table.HideHeaders();
        table.AddColumn("Key");
        table.AddColumn("Value");

        table.AddRow("[bold]Stack:[/]",
            status.Stack ?? "[dim]not set — use /stack set <preset>[/]");
        table.AddRow("[bold]LLM:[/]", status.LlmStatus);
        table.AddRow("[bold]GPU:[/]", status.GpuStatus);
        table.AddRow("[bold]MCP:[/]", status.McpStatus);
        table.AddRow("[bold]ACP:[/]", status.AcpStatus);
        table.AddRow("[bold]Workspace:[/]", status.WorkspacePath);
        table.AddRow("[bold]Limits:[/]", status.LimitsInfo);

        AnsiConsole.Write(table);

        Rule footer = new("[dim]Ready. Awaiting commands.[/]");
        footer.Style = Style.Parse("blue");
        AnsiConsole.Write(footer);
    }
}

/// <summary>
/// Data for the initialization status banner.
/// </summary>
public sealed class InitStatus
{
    /// <summary>Gets or sets the active stack name.</summary>
    public string? Stack { get; set; }

    /// <summary>Gets or sets the LLM status summary.</summary>
    public string LlmStatus { get; set; } = "No providers configured";

    /// <summary>Gets or sets the GPU status summary.</summary>
    public string GpuStatus { get; set; } = "No GPU detected";

    /// <summary>Gets or sets the MCP status summary.</summary>
    public string McpStatus { get; set; } = "0/0 servers";

    /// <summary>Gets or sets the ACP status summary.</summary>
    public string AcpStatus { get; set; } = "0/0 agents";

    /// <summary>Gets or sets the workspace path.</summary>
    public string WorkspacePath { get; set; } = ".";

    /// <summary>Gets or sets the limits info string.</summary>
    public string LimitsInfo { get; set; } = "default";
}
