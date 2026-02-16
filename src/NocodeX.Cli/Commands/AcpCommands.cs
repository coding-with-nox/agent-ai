using System.CommandLine;
using NocodeX.Infrastructure.Acp;

namespace NocodeX.Cli.Commands;

/// <summary>
/// Builds /acp command tree for ACP agent management.
/// </summary>
public static class AcpCommands
{
    /// <summary>
    /// Creates the /acp command hierarchy.
    /// </summary>
    /// <param name="acpManager">ACP client manager.</param>
    /// <returns>Configured command.</returns>
    public static Command Build(AcpClientManager acpManager)
    {
        Command acp = new("acp", "Manage ACP agent connections.");

        Command status = new("status", "Show ACP agent health status.");
        status.SetHandler(async () =>
        {
            var health = await acpManager.CheckAllHealthAsync(CancellationToken.None);
            foreach (var kvp in health)
            {
                string icon = kvp.Value ? "OK" : "FAIL";
                Console.WriteLine($"[{icon}] {kvp.Key}");
            }
        });

        Command agents = new("agents", "List registered ACP agents.");
        agents.SetHandler(() =>
        {
            foreach (string id in acpManager.GetAgentIds())
            {
                Console.WriteLine(id);
            }
        });

        acp.AddCommand(status);
        acp.AddCommand(agents);
        return acp;
    }
}
