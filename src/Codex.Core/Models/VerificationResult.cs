namespace Codex.Core.Models;

/// <summary>
/// Result of a verification step (lint, build, test).
/// </summary>
public sealed record VerificationResult
{
    /// <summary>Gets whether all verification checks passed.</summary>
    public required bool Passed { get; init; }

    /// <summary>Gets the verification step name.</summary>
    public required string StepName { get; init; }

    /// <summary>Gets the standard output from the verification command.</summary>
    public string? StandardOutput { get; init; }

    /// <summary>Gets the standard error from the verification command.</summary>
    public string? StandardError { get; init; }

    /// <summary>Gets the process exit code.</summary>
    public int ExitCode { get; init; }

    /// <summary>Gets the elapsed execution time.</summary>
    public TimeSpan Elapsed { get; init; }
}
