using Codex.Core.Interfaces;
using Codex.Core.Models;
using Microsoft.Extensions.Logging;

namespace Codex.Infrastructure.Mcp;

/// <summary>
/// Manages multiple MCP server client connections.
/// </summary>
public sealed class McpClientManager : IDisposable
{
    private readonly Dictionary<string, IMcpClient> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<McpClientManager> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpClientManager"/> class.
    /// </summary>
    public McpClientManager(
        IEnumerable<IMcpClient> clients,
        ILogger<McpClientManager> logger)
    {
        _logger = logger;
        foreach (IMcpClient client in clients)
        {
            _clients[client.ServerId] = client;
        }
    }

    /// <summary>
    /// Gets an MCP client by server identifier.
    /// </summary>
    /// <param name="serverId">The server identifier.</param>
    /// <returns>The MCP client.</returns>
    public IMcpClient GetClient(string serverId)
    {
        if (!_clients.TryGetValue(serverId, out IMcpClient? client))
        {
            throw new KeyNotFoundException($"MCP server '{serverId}' not registered.");
        }
        return client;
    }

    /// <summary>
    /// Gets all registered server identifiers.
    /// </summary>
    /// <returns>Server IDs.</returns>
    public IReadOnlyList<string> GetServerIds()
    {
        return _clients.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// Checks health of all registered MCP servers.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Health status keyed by server ID.</returns>
    public async Task<IReadOnlyDictionary<string, bool>> CheckAllHealthAsync(CancellationToken ct)
    {
        Dictionary<string, bool> results = new();
        foreach (KeyValuePair<string, IMcpClient> kvp in _clients)
        {
            try
            {
                bool healthy = await kvp.Value.CheckHealthAsync(ct);
                results[kvp.Key] = healthy;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MCP health check failed for {Server}", kvp.Key);
                results[kvp.Key] = false;
            }
        }
        return results;
    }

    /// <summary>
    /// Disposes all managed MCP clients.
    /// </summary>
    public void Dispose()
    {
        foreach (IMcpClient client in _clients.Values)
        {
            if (client is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
