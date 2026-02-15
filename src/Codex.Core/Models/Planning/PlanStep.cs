using Codex.Core.Enums;

namespace Codex.Core.Models.Planning;

/// <summary>
/// A single step within an execution plan.
/// </summary>
public sealed record PlanStep
{
    /// <summary>Gets the step identifier.</summary>
    public required string StepId { get; init; }

    /// <summary>Gets the command or action to execute.</summary>
    public required string Command { get; init; }

    /// <summary>Gets step IDs that must complete before this step runs.</summary>
    public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();

    /// <summary>Gets the current execution status.</summary>
    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    /// <summary>Gets or sets the step output after execution.</summary>
    public string? Output { get; set; }

    /// <summary>Gets or sets the error message if the step failed.</summary>
    public string? Error { get; set; }
}
