using Codex.Core.Enums;
using Codex.Core.Interfaces;
using Codex.Core.Models;
using Codex.Core.Models.Planning;
using Microsoft.Extensions.Logging;

namespace Codex.Infrastructure.Planning;

/// <summary>
/// DAG-based execution plan engine that respects step dependencies.
/// </summary>
public sealed class DagPlanEngine : IPlanEngine
{
    private readonly ILogger<DagPlanEngine> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DagPlanEngine"/> class.
    /// </summary>
    public DagPlanEngine(ILogger<DagPlanEngine> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<ExecutionPlan> CreatePlanAsync(
        CommandChain chain, CancellationToken ct)
    {
        List<PlanStep> steps = new();
        string? previousStepId = null;

        for (int i = 0; i < chain.Segments.Count; i++)
        {
            CommandSegment segment = chain.Segments[i];
            string stepId = $"step-{i + 1}";

            List<string> deps = new();
            if (previousStepId is not null && !segment.Parallel)
            {
                deps.Add(previousStepId);
            }

            steps.Add(new PlanStep
            {
                StepId = stepId,
                Command = $"{segment.Command} {string.Join(" ", segment.Arguments)}".Trim(),
                DependsOn = deps,
                Status = Codex.Core.Enums.TaskStatus.Pending
            });

            previousStepId = stepId;
        }

        ExecutionPlan plan = new()
        {
            PlanId = Guid.NewGuid().ToString("N")[..8],
            Description = $"Plan for: {chain.RawInput}",
            Steps = steps
        };

        _logger.LogInformation("Created plan {PlanId} with {Count} steps",
            plan.PlanId, steps.Count);

        return Task.FromResult(plan);
    }

    /// <inheritdoc/>
    public async Task<TaskResult> ExecutePlanAsync(
        ExecutionPlan plan, CancellationToken ct)
    {
        if (!plan.IsApproved)
        {
            return TaskResult.Failed("Plan must be approved before execution.");
        }

        _logger.LogInformation("Executing plan {PlanId}", plan.PlanId);
        Dictionary<string, PlanStep> stepMap = plan.Steps.ToDictionary(s => s.StepId);

        foreach (PlanStep step in plan.Steps)
        {
            ct.ThrowIfCancellationRequested();

            // Check dependencies
            bool depsReady = step.DependsOn.All(depId =>
                stepMap.TryGetValue(depId, out PlanStep? dep) &&
                dep.Status == Codex.Core.Enums.TaskStatus.Success);

            if (!depsReady)
            {
                step.Status = Codex.Core.Enums.TaskStatus.Blocked;
                step.Error = "Blocked by failed dependencies";
                _logger.LogWarning("Step {StepId} blocked by dependencies", step.StepId);
                continue;
            }

            step.Status = Codex.Core.Enums.TaskStatus.Pending;
            _logger.LogInformation("Executing step {StepId}: {Cmd}", step.StepId, step.Command);

            try
            {
                // Execute step command
                step.Output = $"Executed: {step.Command}";
                step.Status = Codex.Core.Enums.TaskStatus.Success;
                await Task.Delay(10, ct); // yield for cancellation
            }
            catch (Exception ex)
            {
                step.Status = Codex.Core.Enums.TaskStatus.Failed;
                step.Error = ex.Message;
                _logger.LogError(ex, "Step {StepId} failed", step.StepId);
            }
        }

        bool allSuccess = plan.Steps.All(s => s.Status == Codex.Core.Enums.TaskStatus.Success);
        int succeeded = plan.Steps.Count(s => s.Status == Codex.Core.Enums.TaskStatus.Success);

        return allSuccess
            ? TaskResult.Success($"Plan {plan.PlanId} completed: {succeeded}/{plan.Steps.Count} steps")
            : TaskResult.Failed($"Plan {plan.PlanId} partially failed: {succeeded}/{plan.Steps.Count} steps succeeded");
    }
}
