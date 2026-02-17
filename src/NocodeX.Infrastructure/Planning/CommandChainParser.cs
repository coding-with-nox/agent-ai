
using NocodeX.Core.Models;
using Microsoft.Extensions.Logging;

namespace NocodeX.Infrastructure.Planning;

/// <summary>
/// Parses raw command input into structured command chains.
/// </summary>
public sealed class CommandChainParser
{
    private readonly ILogger<CommandChainParser> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandChainParser"/> class.
    /// </summary>
    public CommandChainParser(ILogger<CommandChainParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses a raw command string into a command chain.
    /// </summary>
    /// <param name="input">Raw input like "gen:endpoint POST /api/orders &amp;&amp; gen:test Orders".</param>
    /// <returns>Structured command chain.</returns>
    public CommandChain Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new CommandChain
            {
                RawInput = input ?? string.Empty,
                Segments = Array.Empty<CommandSegment>()
            };
        }

        List<CommandSegment> segments = [];

        // Split by && (sequential) or & (parallel)
        string[] sequentialParts = input.Split("&&", StringSplitOptions.TrimEntries);

        foreach (string part in sequentialParts)
        {
            string[] parallelParts = part.Split('&', StringSplitOptions.TrimEntries);

            for (int i = 0; i < parallelParts.Length; i++)
            {
                string raw = parallelParts[i].Trim();
                if (string.IsNullOrEmpty(raw)) continue;

                string[] tokens = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string command = tokens[0];
                string[] args = tokens.Length > 1 ? tokens[1..] : Array.Empty<string>();

                segments.Add(new CommandSegment
                {
                    Command = command,
                    Arguments = args,
                    Parallel = i < parallelParts.Length - 1 && parallelParts.Length > 1
                });
            }
        }

        _logger.LogDebug("Parsed {Count} command segments from: {Input}", segments.Count, input);

        return new CommandChain
        {
            RawInput = input,
            Segments = segments
        };
    }
}
