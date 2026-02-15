using Codex.Core.Models;
using Microsoft.Extensions.Logging;

namespace Codex.Infrastructure.CodeGeneration;

/// <summary>
/// Maps parsed code blocks to file system paths based on stack conventions.
/// </summary>
public sealed class FileOutputMapper
{
    private readonly string _workspaceRoot;
    private readonly ILogger<FileOutputMapper> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileOutputMapper"/> class.
    /// </summary>
    /// <param name="workspaceRoot">Absolute path to the workspace root.</param>
    /// <param name="logger">Logger instance.</param>
    public FileOutputMapper(string workspaceRoot, ILogger<FileOutputMapper> logger)
    {
        _workspaceRoot = workspaceRoot;
        _logger = logger;
    }

    /// <summary>
    /// Resolves a relative file path to an absolute path within the workspace.
    /// </summary>
    /// <param name="relativePath">The relative path from the LLM output.</param>
    /// <param name="stack">Active stack configuration.</param>
    /// <returns>The resolved absolute file path.</returns>
    public string ResolveFilePath(string relativePath, StackConfig stack)
    {
        // Normalize separators
        string normalized = relativePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        // Prevent path traversal
        if (normalized.Contains(".." + Path.DirectorySeparatorChar) ||
            normalized.StartsWith(".."))
        {
            _logger.LogWarning("Blocked path traversal attempt: {Path}", relativePath);
            normalized = Path.GetFileName(normalized);
        }

        string fullPath = Path.Combine(_workspaceRoot, normalized);

        // Ensure directory exists
        string? directory = Path.GetDirectoryName(fullPath);
        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogDebug("Created directory: {Dir}", directory);
        }

        _logger.LogDebug("Mapped {Relative} → {Full}", relativePath, fullPath);
        return fullPath;
    }

    /// <summary>
    /// Writes generated files to disk.
    /// </summary>
    /// <param name="files">Files keyed by absolute path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of written file paths.</returns>
    public async Task<IReadOnlyList<string>> WriteFilesAsync(
        IReadOnlyDictionary<string, string> files,
        CancellationToken ct)
    {
        List<string> written = new();

        foreach (KeyValuePair<string, string> file in files)
        {
            ct.ThrowIfCancellationRequested();
            await File.WriteAllTextAsync(file.Key, file.Value, ct);
            written.Add(file.Key);
            _logger.LogInformation("Wrote file: {Path}", file.Key);
        }

        return written;
    }
}
