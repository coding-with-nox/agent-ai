namespace Codex.Core.Enums;

/// <summary>
/// Represents the lifecycle state of an executed command.
/// </summary>
public enum TaskStatus
{
    Pending,
    Success,
    Failed,
    Blocked
}
