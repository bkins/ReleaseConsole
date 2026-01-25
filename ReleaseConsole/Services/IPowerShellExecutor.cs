namespace ReleaseConsole.Services;

public interface IPowerShellExecutor
{
    Task<PowerShellResult> ExecuteScriptAsync(string scriptPath, Dictionary<string, string>? parameters = null, CancellationToken ct = default);
}