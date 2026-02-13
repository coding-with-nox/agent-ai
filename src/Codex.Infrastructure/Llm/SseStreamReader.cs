using System.Runtime.CompilerServices;
using System.Text;

namespace Codex.Infrastructure.Llm;

/// <summary>
/// Reads SSE data chunks from an HTTP stream.
/// </summary>
public static class SseStreamReader
{
    /// <summary>
    /// Reads SSE <c>data:</c> payloads as raw strings.
    /// </summary>
    /// <param name="stream">HTTP content stream.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Sequence of SSE data values.</returns>
    public static async IAsyncEnumerable<string> ReadDataAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using StreamReader reader = new(stream, Encoding.UTF8);
        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            yield return line[5..].Trim();
        }
    }
}
