namespace Codex.Core.Interfaces;

/// <summary>
/// Structured audit logging for agent operations.
/// </summary>
public interface IAuditLog
{
    /// <summary>
    /// Records an audit event.
    /// </summary>
    /// <param name="entry">The audit log entry.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RecordAsync(AuditEntry entry, CancellationToken ct);

    /// <summary>
    /// Retrieves recent audit entries.
    /// </summary>
    /// <param name="count">Maximum entries to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Recent audit entries.</returns>
    Task<IReadOnlyList<AuditEntry>> GetRecentAsync(
        int count, CancellationToken ct);
}

/// <summary>
/// A single audit log entry recording an agent action.
/// </summary>
public sealed record AuditEntry
{
    /// <summary>Gets the timestamp of the event.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets the event category (llm, mcp, acp, gen, verify).</summary>
    public required string Category { get; init; }

    /// <summary>Gets the action performed.</summary>
    public required string Action { get; init; }

    /// <summary>Gets additional details about the event.</summary>
    public string? Details { get; init; }

    /// <summary>Gets whether the action succeeded.</summary>
    public bool Success { get; init; } = true;

    /// <summary>Gets the elapsed time for the action.</summary>
    public TimeSpan? Elapsed { get; init; }
}
