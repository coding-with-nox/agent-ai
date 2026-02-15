using System.Diagnostics;
using System.Text.Json;
using Codex.Core.Exceptions;
using Codex.Core.Interfaces;
using Codex.Core.Models;
using Microsoft.Extensions.Logging;

namespace Codex.Infrastructure.Mcp;

/// <summary>
/// MCP client that communicates with MCP servers via stdio transport.
/// </summary>
public sealed class McpStdioClient : IMcpClient, IDisposable
{
    private readonly McpServerConfig _config;
    private readonly ILogger<McpStdioClient> _logger;
    private Process? _process;
    private int _requestId;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpStdioClient"/> class.
    /// </summary>
    public McpStdioClient(McpServerConfig config, ILogger<McpStdioClient> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string ServerId => _config.Id;

    /// <inheritdoc/>
    public async Task<McpToolResult> InvokeToolAsync(
        string toolName,
        IReadOnlyDictionary<string, string> arguments,
        CancellationToken ct)
    {
        EnsureProcessRunning();

        int id = Interlocked.Increment(ref _requestId);
        var request = new
        {
            jsonrpc = "2.0",
            id,
            method = "tools/call",
            @params = new { name = toolName, arguments }
        };

        string json = JsonSerializer.Serialize(request);
        _logger.LogDebug("MCP [{Server}] Invoking {Tool}", ServerId, toolName);

        try
        {
            await _process!.StandardInput.WriteLineAsync(json.AsMemory(), ct);
            await _process.StandardInput.FlushAsync();

            string? responseLine = await _process.StandardOutput.ReadLineAsync(ct);
            if (responseLine is null)
            {
                throw new McpInvocationException(ServerId, toolName, "No response from server");
            }

            using JsonDocument doc = JsonDocument.Parse(responseLine);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("error", out JsonElement error))
            {
                string errorMsg = error.GetProperty("message").GetString() ?? "Unknown error";
                return new McpToolResult(false, errorMsg, true);
            }

            JsonElement result = root.GetProperty("result");
            string content = result.TryGetProperty("content", out JsonElement contentEl)
                ? contentEl.ToString()
                : result.ToString();

            return new McpToolResult(true, content);
        }
        catch (McpInvocationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP [{Server}] Tool {Tool} invocation failed", ServerId, toolName);
            throw new McpInvocationException(ServerId, toolName, ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListToolsAsync(CancellationToken ct)
    {
        EnsureProcessRunning();

        int id = Interlocked.Increment(ref _requestId);
        var request = new { jsonrpc = "2.0", id, method = "tools/list" };
        string json = JsonSerializer.Serialize(request);

        try
        {
            await _process!.StandardInput.WriteLineAsync(json.AsMemory(), ct);
            await _process.StandardInput.FlushAsync();

            string? responseLine = await _process.StandardOutput.ReadLineAsync(ct);
            if (responseLine is null) return Array.Empty<string>();

            using JsonDocument doc = JsonDocument.Parse(responseLine);
            JsonElement tools = doc.RootElement.GetProperty("result").GetProperty("tools");

            List<string> names = new();
            foreach (JsonElement tool in tools.EnumerateArray())
            {
                string? name = tool.GetProperty("name").GetString();
                if (name is not null) names.Add(name);
            }

            return names;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP [{Server}] ListTools failed", ServerId);
            return Array.Empty<string>();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            EnsureProcessRunning();
            IReadOnlyList<string> tools = await ListToolsAsync(ct);
            return tools.Count > 0 || (_process is not null && !_process.HasExited);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Disposes the underlying process.
    /// </summary>
    public void Dispose()
    {
        if (_process is not null && !_process.HasExited)
        {
            try { _process.Kill(); } catch { /* best effort */ }
        }
        _process?.Dispose();
    }

    private void EnsureProcessRunning()
    {
        if (_process is not null && !_process.HasExited) return;

        string[] parts = _config.Endpoint.Split(' ', 2);
        string fileName = parts[0];
        string arguments = parts.Length > 1 ? parts[1] : string.Empty;

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _process.Start();
        _logger.LogInformation("MCP [{Server}] Process started: {File} {Args}",
            ServerId, fileName, arguments);
    }
}
