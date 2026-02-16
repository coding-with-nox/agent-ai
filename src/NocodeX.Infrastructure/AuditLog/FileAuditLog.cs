using System.Text.Json;
using NocodeX.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace NocodeX.Infrastructure.AuditLog;

/// <summary>
/// File-based audit log implementation using JSON-Lines format.
/// </summary>
public sealed class FileAuditLog : IAuditLog
{
    private readonly string _logFilePath;
    private readonly ILogger<FileAuditLog> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="FileAuditLog"/> class.
    /// </summary>
    /// <param name="logFilePath">Path to the JSON-Lines audit log file.</param>
    /// <param name="logger">Logger instance.</param>
    public FileAuditLog(string logFilePath, ILogger<FileAuditLog> logger)
    {
        _logFilePath = logFilePath;
        _logger = logger;

        string? dir = Path.GetDirectoryName(logFilePath);
        if (dir is not null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    /// <inheritdoc/>
    public async Task RecordAsync(AuditEntry entry, CancellationToken ct)
    {
        string line = JsonSerializer.Serialize(entry, JsonOpts);

        await _writeLock.WaitAsync(ct);
        try
        {
            await File.AppendAllTextAsync(_logFilePath, line + Environment.NewLine, ct);
        }
        finally
        {
            _writeLock.Release();
        }

        _logger.LogDebug("Audit: [{Category}] {Action} ({Success})",
            entry.Category, entry.Action, entry.Success ? "ok" : "fail");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuditEntry>> GetRecentAsync(
        int count, CancellationToken ct)
    {
        if (!File.Exists(_logFilePath))
        {
            return Array.Empty<AuditEntry>();
        }

        string[] lines = await File.ReadAllLinesAsync(_logFilePath, ct);
        List<AuditEntry> entries = new();

        int start = Math.Max(0, lines.Length - count);
        for (int i = start; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            try
            {
                AuditEntry? entry = JsonSerializer.Deserialize<AuditEntry>(lines[i], JsonOpts);
                if (entry is not null) entries.Add(entry);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse audit entry at line {Line}", i + 1);
            }
        }

        return entries;
    }
}
