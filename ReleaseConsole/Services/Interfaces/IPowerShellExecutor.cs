using ReleaseConsole.Core;

namespace ReleaseConsole.Services.Interfaces;

public interface IPowerShellExecutor
{
    Task<PowerShellResult> ExecuteScriptAsync( string                      scriptPath
                                             , ComponentType?              component
                                             , Dictionary<string, string>? parameters = null
                                             , CancellationToken           ct         = default );
    ScriptOutputMode       OutputMode { get; set; }
}