using System.Text.Json;
using ReleaseConsole.Core;

namespace ReleaseConsole.Services;

public sealed class FileAuditLog : IAuditLog
{
    private readonly        string                _logPath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public FileAuditLog(string? logPath = null)
    {
        var defaultPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "ReleaseConsole",
            "audit.log"
        );

        _logPath = logPath ?? defaultPath;
        
        var directory = Path.GetDirectoryName(_logPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }

    public async Task LogAsync(AuditEntry entry, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(entry, JsonOptions);
        await File.AppendAllTextAsync(_logPath, json + System.Environment.NewLine, ct);
    }

    public async Task<IReadOnlyList<AuditEntry>> GetRecentEntriesAsync(int count = 50, CancellationToken ct = default)
    {
        if (!File.Exists(_logPath))
            return Array.Empty<AuditEntry>();

        var lines   = await File.ReadAllLinesAsync(_logPath, ct);
        var entries = new List<AuditEntry>();

        foreach (var line in lines.Reverse().Take(count))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var entry = JsonSerializer.Deserialize<AuditEntry>(line);
            if (entry is not null)
                entries.Add(entry);
        }

        return entries;
    }
}