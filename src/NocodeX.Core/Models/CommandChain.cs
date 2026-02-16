namespace NocodeX.Core.Models;

/// <summary>
/// A parsed chain of commands to be executed in sequence or parallel.
/// </summary>
public sealed record CommandChain
{
    /// <summary>Gets the raw input that was parsed into this chain.</summary>
    public required string RawInput { get; init; }

    /// <summary>Gets the ordered list of command segments.</summary>
    public required IReadOnlyList<CommandSegment> Segments { get; init; }
}

/// <summary>
/// A single command segment within a command chain.
/// </summary>
public sealed record CommandSegment
{
    /// <summary>Gets the command name (e.g. gen:endpoint, stack set).</summary>
    public required string Command { get; init; }

    /// <summary>Gets the command arguments.</summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>Gets whether this segment runs in parallel with the next.</summary>
    public bool Parallel { get; init; }
}
