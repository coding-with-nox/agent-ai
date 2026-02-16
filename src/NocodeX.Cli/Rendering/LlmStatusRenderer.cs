using NocodeX.Core.Models.Llm;
using Spectre.Console;

namespace NocodeX.Cli.Rendering;

/// <summary>
/// Renders LLM provider status with GPU bars and token speed gauges.
/// </summary>
public static class LlmStatusRenderer
{
    /// <summary>
    /// Renders a full status table for all LLM providers.
    /// </summary>
    /// <param name="statuses">Health status per provider.</param>
    public static void Render(IReadOnlyDictionary<string, LlmHealthStatus> statuses)
    {
        Table table = new();
        table.Border(TableBorder.Rounded);
        table.Title("[bold]LLM Provider Status[/]");
        table.AddColumn("Provider");
        table.AddColumn("Status");
        table.AddColumn("Model");
        table.AddColumn("GPU %");
        table.AddColumn("VRAM Free");
        table.AddColumn("Tok/s");

        foreach (KeyValuePair<string, LlmHealthStatus> kvp in statuses)
        {
            string status = kvp.Value.IsReachable && kvp.Value.IsModelLoaded
                ? "[green]Healthy[/]"
                : kvp.Value.IsReachable
                    ? "[yellow]No Model[/]"
                    : "[red]Offline[/]";

            string model = kvp.Value.ActiveModel ?? "-";
            string gpu = kvp.Value.GpuUtilizationPercent.HasValue
                ? $"{kvp.Value.GpuUtilizationPercent:F0}%"
                : "-";
            string vram = kvp.Value.VramFreeMb.HasValue
                ? $"{kvp.Value.VramFreeMb} MB"
                : "-";
            string tps = kvp.Value.AverageTokensPerSecond.HasValue
                ? $"{kvp.Value.AverageTokensPerSecond:F1}"
                : "-";

            table.AddRow(kvp.Key, status, model, gpu, vram, tps);
        }

        AnsiConsole.Write(table);

        // Render GPU utilization bar if available
        LlmHealthStatus? withGpu = statuses.Values
            .FirstOrDefault(s => s.GpuUtilizationPercent.HasValue);

        if (withGpu?.GpuUtilizationPercent is not null)
        {
            double pct = withGpu.GpuUtilizationPercent.Value;
            Color barColor = pct < 50 ? Color.Green : pct < 80 ? Color.Yellow : Color.Red;

            AnsiConsole.Write(new BreakdownChart()
                .Width(60)
                .AddItem("Used", pct, barColor)
                .AddItem("Free", 100 - pct, Color.Grey));
        }
    }
}
