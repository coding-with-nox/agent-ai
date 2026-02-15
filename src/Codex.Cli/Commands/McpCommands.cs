using System.CommandLine;
using Codex.Infrastructure.Mcp;

namespace Codex.Cli.Commands;

/// <summary>
/// Builds /mcp command tree for MCP server management.
/// </summary>
public static class McpCommands
{
    /// <summary>
    /// Creates the /mcp command hierarchy.
    /// </summary>
    /// <param name="mcpManager">MCP client manager.</param>
    /// <returns>Configured command.</returns>
    public static Command Build(McpClientManager mcpManager)
    {
        Command mcp = new("mcp", "Manage MCP server connections.");

        Command status = new("status", "Show MCP server health status.");
        status.SetHandler(async () =>
        {
            var health = await mcpManager.CheckAllHealthAsync(CancellationToken.None);
            foreach (var kvp in health)
            {
                string icon = kvp.Value ? "OK" : "FAIL";
                Console.WriteLine($"[{icon}] {kvp.Key}");
            }
        });

        Command servers = new("servers", "List registered MCP servers.");
        servers.SetHandler(() =>
        {
            foreach (string id in mcpManager.GetServerIds())
            {
                Console.WriteLine(id);
            }
        });

        Command tools = new("tools", "List tools available on a server.");
        Argument<string> serverArg = new("server_id", "MCP server identifier.");
        tools.AddArgument(serverArg);
        tools.SetHandler(async (string serverId) =>
        {
            var client = mcpManager.GetClient(serverId);
            var toolList = await client.ListToolsAsync(CancellationToken.None);
            foreach (string tool in toolList)
            {
                Console.WriteLine($"  {tool}");
            }
        }, serverArg);

        mcp.AddCommand(status);
        mcp.AddCommand(servers);
        mcp.AddCommand(tools);
        return mcp;
    }
}
