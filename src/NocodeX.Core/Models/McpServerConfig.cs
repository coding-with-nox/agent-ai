namespace NocodeX.Core.Models;

/// <summary>
/// Defines a configured MCP server.
/// </summary>
/// <param name="Id">Stable identifier for runtime lookup.</param>
/// <param name="Transport">Transport type (stdio/http/sse).</param>
/// <param name="Endpoint">Endpoint URL or executable path.</param>
/// <param name="Enabled">Whether the server is enabled.</param>
public sealed record McpServerConfig(string Id, string Transport, string Endpoint, bool Enabled = true);
