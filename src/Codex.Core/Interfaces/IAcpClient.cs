using Codex.Core.Models;

namespace Codex.Core.Interfaces;

/// <summary>
/// Client for dispatching tasks to ACP (Agent Communication Protocol) agents.
/// </summary>
public interface IAcpClient
{
    /// <summary>Gets the ACP agent identifier.</summary>
    string AgentId { get; }

    /// <summary>
    /// Dispatches a task to the ACP agent.
    /// </summary>
    /// <param name="task">Task description and context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The agent's task result.</returns>
    Task<AcpTaskResult> DispatchAsync(
        AcpTaskRequest task,
        CancellationToken ct);

    /// <summary>
    /// Checks if the ACP agent is healthy and reachable.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if healthy.</returns>
    Task<bool> CheckHealthAsync(CancellationToken ct);
}

/// <summary>
/// Request payload for an ACP agent task.
/// </summary>
public sealed record AcpTaskRequest
{
    /// <summary>Gets the task description.</summary>
    public required string Description { get; init; }

    /// <summary>Gets the context files keyed by path.</summary>
    public IReadOnlyDictionary<string, string>? ContextFiles { get; init; }

    /// <summary>Gets the LLM provider to use for inference.</summary>
    public string? LlmProviderId { get; init; }
}

/// <summary>
/// Result from an ACP agent task execution.
/// </summary>
public sealed record AcpTaskResult
{
    /// <summary>Gets whether the task succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Gets the result content or feedback.</summary>
    public required string Content { get; init; }

    /// <summary>Gets any suggested code changes.</summary>
    public IReadOnlyDictionary<string, string>? SuggestedChanges { get; init; }
}
