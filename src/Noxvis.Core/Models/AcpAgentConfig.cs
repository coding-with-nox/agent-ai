using Noxvis.Core.Enums;

namespace Noxvis.Core.Models;

/// <summary>
/// Defines a configured ACP agent.
/// </summary>
/// <param name="Id">Stable identifier for runtime lookup.</param>
/// <param name="Endpoint">Endpoint URL for the ACP agent.</param>
/// <param name="TrustLevel">Trust level used by enforcement middleware.</param>
/// <param name="Enabled">Whether the agent is enabled.</param>
public sealed record AcpAgentConfig(string Id, string Endpoint, TrustLevel TrustLevel, bool Enabled = true);
