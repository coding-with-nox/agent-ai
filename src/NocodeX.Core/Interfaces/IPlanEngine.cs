using NocodeX.Core.Models;
using NocodeX.Core.Models.Planning;

namespace NocodeX.Core.Interfaces;

/// <summary>
/// Engine for creating and executing DAG-based execution plans.
/// </summary>
public interface IPlanEngine
{
    /// <summary>
    /// Creates an execution plan from a command chain.
    /// </summary>
    /// <param name="chain">Parsed command chain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated execution plan.</returns>
    Task<ExecutionPlan> CreatePlanAsync(
        CommandChain chain, CancellationToken ct);

    /// <summary>
    /// Executes an approved plan step by step, respecting dependencies.
    /// </summary>
    /// <param name="plan">The plan to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Final task result after all steps complete.</returns>
    Task<TaskResult> ExecutePlanAsync(
        ExecutionPlan plan, CancellationToken ct);
}
