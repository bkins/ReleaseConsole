using ReleaseConsole.Core;

public interface IAuditLog
{
    Task                            LogAsync(AuditEntry       entry,      CancellationToken ct = default);
    Task<IReadOnlyList<AuditEntry>> GetRecentEntriesAsync(int count = 50, CancellationToken ct = default);
}