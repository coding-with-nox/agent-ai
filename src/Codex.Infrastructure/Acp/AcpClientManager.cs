using Codex.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Codex.Infrastructure.Acp;

/// <summary>
/// Manages multiple ACP agent client instances.
/// </summary>
public sealed class AcpClientManager
{
    private readonly Dictionary<string, IAcpClient> _agents = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<AcpClientManager> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AcpClientManager"/> class.
    /// </summary>
    public AcpClientManager(
        IEnumerable<IAcpClient> agents,
        ILogger<AcpClientManager> logger)
    {
        _logger = logger;
        foreach (IAcpClient agent in agents)
        {
            _agents[agent.AgentId] = agent;
        }
    }

    /// <summary>
    /// Gets an ACP agent client by identifier.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <returns>The ACP client.</returns>
    public IAcpClient GetAgent(string agentId)
    {
        if (!_agents.TryGetValue(agentId, out IAcpClient? agent))
        {
            throw new KeyNotFoundException($"ACP agent '{agentId}' not registered.");
        }
        return agent;
    }

    /// <summary>
    /// Gets all registered agent identifiers.
    /// </summary>
    /// <returns>Agent IDs.</returns>
    public IReadOnlyList<string> GetAgentIds()
    {
        return _agents.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// Checks health of all registered ACP agents.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Health status keyed by agent ID.</returns>
    public async Task<IReadOnlyDictionary<string, bool>> CheckAllHealthAsync(CancellationToken ct)
    {
        Dictionary<string, bool> results = new();
        foreach (KeyValuePair<string, IAcpClient> kvp in _agents)
        {
            try
            {
                bool healthy = await kvp.Value.CheckHealthAsync(ct);
                results[kvp.Key] = healthy;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ACP health check failed for {Agent}", kvp.Key);
                results[kvp.Key] = false;
            }
        }
        return results;
    }
}
