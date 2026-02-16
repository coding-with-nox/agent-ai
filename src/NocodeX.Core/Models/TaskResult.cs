using TaskStatus = NocodeX.Core.Enums.TaskStatus;

namespace NocodeX.Core.Models;

/// <summary>
/// Represents a standardized command execution result.
/// </summary>
public sealed record TaskResult(TaskStatus Status, string Message, IReadOnlyList<string> OutputFiles)
{
    /// <summary>
    /// Creates a successful task result.
    /// </summary>
    /// <param name="message">Completion message.</param>
    /// <param name="outputFiles">Generated or modified files.</param>
    /// <returns>A successful result payload.</returns>
    public static TaskResult Success(string message, IReadOnlyList<string>? outputFiles = null)
    {
        IReadOnlyList<string> files = outputFiles ?? Array.Empty<string>();
        return new TaskResult(TaskStatus.Success, message, files);
    }

    /// <summary>
    /// Creates a failed task result.
    /// </summary>
    /// <param name="message">Error message.</param>
    /// <returns>A failed result payload.</returns>
    public static TaskResult Failed(string message)
    {
        return new TaskResult(TaskStatus.Failed, message, Array.Empty<string>());
    }
}
