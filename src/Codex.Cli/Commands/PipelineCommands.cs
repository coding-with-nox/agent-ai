using System.CommandLine;
using Codex.Cli.Rendering;
using Codex.Core.Interfaces;
using Codex.Core.Models;
using Codex.Core.Models.Planning;
using Codex.Infrastructure.Planning;

namespace Codex.Cli.Commands;

/// <summary>
/// Builds /plan and /approve command tree for execution planning.
/// </summary>
public static class PipelineCommands
{
    private static ExecutionPlan? _currentPlan;

    /// <summary>
    /// Creates the /plan command.
    /// </summary>
    /// <param name="parser">Command chain parser.</param>
    /// <param name="planEngine">Plan engine.</param>
    /// <returns>Configured command.</returns>
    public static Command BuildPlanCommand(
        CommandChainParser parser,
        IPlanEngine planEngine)
    {
        Command plan = new("plan", "Create an execution plan from a command chain.");
        Argument<string> inputArg = new("commands", "Command chain to plan.");
        plan.AddArgument(inputArg);

        plan.SetHandler(async (string input) =>
        {
            CommandChain chain = parser.Parse(input);
            _currentPlan = await planEngine.CreatePlanAsync(chain, CancellationToken.None);
            PlanRenderer.Render(_currentPlan);
            Console.WriteLine("Run '/approve' to execute this plan.");
        }, inputArg);

        return plan;
    }

    /// <summary>
    /// Creates the /approve command.
    /// </summary>
    /// <param name="planEngine">Plan engine.</param>
    /// <returns>Configured command.</returns>
    public static Command BuildApproveCommand(IPlanEngine planEngine)
    {
        Command approve = new("approve", "Approve and execute the current plan.");

        approve.SetHandler(async () =>
        {
            if (_currentPlan is null)
            {
                Console.WriteLine("No plan to approve. Run '/plan <commands>' first.");
                return;
            }

            _currentPlan.IsApproved = true;
            TaskResult result = await planEngine.ExecutePlanAsync(
                _currentPlan, CancellationToken.None);

            ResultRenderer.Render(result);
            _currentPlan = null;
        });

        return approve;
    }
}
