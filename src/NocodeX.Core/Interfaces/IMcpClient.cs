using NocodeX.Core.Models;

namespace NocodeX.Core.Interfaces;

/// <summary>
/// Client for invoking MCP (Model Context Protocol) server tools.
/// </summary>
public interface IMcpClient
{
    /// <summary>Gets the MCP server identifier.</summary>
    string ServerId { get; }

    /// <summary>
    /// Invokes a tool on the MCP server.
    /// </summary>
    /// <param name="toolName">The tool to invoke.</param>
    /// <param name="arguments">Tool arguments as key-value pairs.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tool execution result.</returns>
    Task<McpToolResult> InvokeToolAsync(
        string toolName,
        IReadOnlyDictionary<string, string> arguments,
        CancellationToken ct);

    /// <summary>
    /// Lists available tools on the MCP server.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Available tool names.</returns>
    Task<IReadOnlyList<string>> ListToolsAsync(CancellationToken ct);

    /// <summary>
    /// Checks if the MCP server is healthy and reachable.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if healthy.</returns>
    Task<bool> CheckHealthAsync(CancellationToken ct);
}

/// <summary>
/// Result from an MCP tool invocation.
/// </summary>
/// <param name="Success">Whether the invocation succeeded.</param>
/// <param name="Content">Result content or error message.</param>
/// <param name="IsError">Whether the result represents an error.</param>
public sealed record McpToolResult(bool Success, string Content, bool IsError = false);
