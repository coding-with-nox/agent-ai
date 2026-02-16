namespace NocodeX.Core.Exceptions;

/// <summary>
/// Thrown when an MCP tool invocation fails.
/// </summary>
public sealed class McpInvocationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="McpInvocationException"/> class.
    /// </summary>
    /// <param name="serverId">The MCP server identifier.</param>
    /// <param name="toolName">The tool that failed.</param>
    /// <param name="message">Error message.</param>
    public McpInvocationException(string serverId, string toolName, string message)
        : base($"MCP tool '{toolName}' on server '{serverId}' failed: {message}")
    {
        ServerId = serverId;
        ToolName = toolName;
    }

    /// <summary>Gets the MCP server identifier.</summary>
    public string ServerId { get; }

    /// <summary>Gets the tool name that failed.</summary>
    public string ToolName { get; }
}
