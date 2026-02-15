namespace Codex.Core.Models.Planning;

/// <summary>
/// A DAG-based execution plan comprising ordered steps.
/// </summary>
public sealed record ExecutionPlan
{
    /// <summary>Gets the unique plan identifier.</summary>
    public required string PlanId { get; init; }

    /// <summary>Gets the human-readable plan description.</summary>
    public required string Description { get; init; }

    /// <summary>Gets the ordered list of plan steps.</summary>
    public required IReadOnlyList<PlanStep> Steps { get; init; }

    /// <summary>Gets the timestamp when the plan was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets whether the plan has been approved for execution.</summary>
    public bool IsApproved { get; set; }
}
