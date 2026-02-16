using NocodeX.Core.Models.Llm;
using Spectre.Console;

namespace NocodeX.Cli.Rendering;

/// <summary>
/// Renders streaming LLM tokens live in the terminal.
/// </summary>
public static class StreamingCodeRenderer
{
    /// <summary>
    /// Streams token chunks to the console with live rendering.
    /// </summary>
    /// <param name="chunks">Async stream of token chunks.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The complete assembled response text.</returns>
    public static async Task<string> RenderStreamAsync(
        IAsyncEnumerable<LlmTokenChunk> chunks,
        CancellationToken ct)
    {
        System.Text.StringBuilder buffer = new();
        int tokenCount = 0;
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        AnsiConsole.MarkupLine("[dim]Generating...[/]");

        await foreach (LlmTokenChunk chunk in chunks.WithCancellation(ct))
        {
            buffer.Append(chunk.Token);
            tokenCount++;

            // Write raw token to console for live effect
            Console.Write(chunk.Token);

            if (chunk.IsComplete)
            {
                sw.Stop();
                Console.WriteLine();
                double tps = sw.Elapsed.TotalSeconds > 0
                    ? tokenCount / sw.Elapsed.TotalSeconds
                    : 0;

                AnsiConsole.MarkupLine(
                    $"[dim]Done: {tokenCount} tokens in {sw.Elapsed.TotalSeconds:F1}s ({tps:F1} tok/s)[/]");

                if (chunk.FinalUsage is not null)
                {
                    AnsiConsole.MarkupLine(
                        $"[dim]Prompt: {chunk.FinalUsage.PromptTokens} | " +
                        $"Completion: {chunk.FinalUsage.CompletionTokens} | " +
                        $"Total: {chunk.FinalUsage.TotalTokens}[/]");
                }
            }
        }

        return buffer.ToString();
    }
}
