using CP.Client.Core.Avails;
using Microsoft.Extensions.Logging;
using ReleaseConsole.Core;
using ReleaseConsole.Menu;
using ReleaseConsole.Services;
using ReleaseConsole.Services.Interfaces;
using Spectre.Console;
using Environment = System.Environment;

namespace ReleaseConsole.Commands;

public class SwapDbCommand : CommandBaseCommand
{
    public override string Name         => "DbSwap";
    public override string Description  => "Copies the Prod DB to Dev. Restores Dev to original state.";
    public          string SpinnerText  { get; set; }
    public          Color? SpinnerColor { get; set; }

    private readonly IPowerShellExecutor _psExecutor;
    private readonly string              _menuTextDbActions;

    public SwapDbCommand( IAuditLog           auditLog
                        , ILogger             logger
                        , IPowerShellExecutor psExecutor
                        , string              menuTextDbActions )
            : base(auditLog
                 , logger)
    {
        _psExecutor        = psExecutor;
        _menuTextDbActions = menuTextDbActions;

        if (_menuTextDbActions == MenuText.DbActions.SwapDb)
        {
            SpinnerText = "Swapping...";
            SpinnerColor = Color.Green;
        }
        else
        {
            SpinnerText = "Restoring...";
            SpinnerColor = Color.Yellow;
        }
    }

    protected override async Task<CommandResult> ExecuteInternalAsync( CancellationToken ct )
    {
        var scriptPath = GetBuildScriptPath();
        
        if (File.Exists(scriptPath)
                .Not())
        {
            return CommandResult.Fail($"DB Action script not found: {scriptPath}");
        }

        var psResult = new PowerShellResult(false
                                          , "Unknown Error"
                                          , "Unknown Error Occurred!"
                                          , -1);
        psResult = await _psExecutor.ExecuteScriptAsync(scriptPath
                                                      , null
                                                      , null
                                                      , ct);

        if (psResult.Success) return CommandResult.Ok($"DB Action script executed successfully.\nDetails:\n{psResult.Output}");
        
        Logger.LogError("DB Action script failed with exit code {ExitCode}\nDetails:\n{Details}"
                      , psResult.ExitCode
                      , psResult.Output);
        
        return CommandResult.Fail("DB Action script failed"
                                , psResult.Error);


    }

    private string GetBuildScriptPath()
    {
        return Path.Combine(GetDefaultScriptsPath()
                          , _menuTextDbActions == MenuText.DbActions.SwapDb
                                    ? "Swap-ProdDbToDev.ps1"
                                    : "Restore-DevDb.ps1");

    }
}