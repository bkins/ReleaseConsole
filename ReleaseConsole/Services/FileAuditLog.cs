using System.Text;
using System.Text.Json;
using ReleaseConsole.Core;
using ReleaseConsole.Services.Interfaces;

namespace ReleaseConsole.Services;

public sealed class FileAuditLog : IAuditLog
{
    private readonly        string                _logPath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public FileAuditLog(string? logPath = null)
    {
        var defaultPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile)
                                     , "ReleaseConsole"
                                     , "audit.log");

        _logPath = logPath ?? defaultPath;
        
        var directory = Path.GetDirectoryName(_logPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }

    public static void FixAuditLog()
    {
        var defaultPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile)
                                     , "ReleaseConsole"
                                     , "audit.log");
        if (!File.Exists(defaultPath))
            return;

        var fixedLines = new List<string>();
        var buffer     = new StringBuilder();
        var depth      = 0;

        foreach (var line in File.ReadLines(defaultPath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            buffer.AppendLine(line);

            depth += line.Count(c => c == '{');
            depth -= line.Count(c => c == '}');

            if (depth == 0 && buffer.Length > 0)
            {
                var json = buffer.ToString();

                try
                {
                    var entry = JsonSerializer.Deserialize<AuditEntry>(json);
                    if (entry != null)
                    {
                        fixedLines.Add(
                            JsonSerializer.Serialize(entry,
                                                     new JsonSerializerOptions { WriteIndented = false }));
                    }
                }
                catch
                {
                    // swallow bad entries, or log if you want
                }

                buffer.Clear();
            }
        }

        File.WriteAllLines(defaultPath, fixedLines);
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