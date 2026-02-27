using Microsoft.Extensions.Logging;
using ReleaseConsole.Core;
using ReleaseConsole.Services;
using ReleaseConsole.Services.Interfaces;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.Commands;

public abstract class CommandBaseCommand : ICommand, ILogger
{
    protected readonly IAuditLog AuditLog;
    protected readonly ILogger   Logger;

    protected virtual ScriptOutputMode OutputMode => ScriptOutputMode.ErrorsOnly;
    
    protected CommandBaseCommand(IAuditLog auditLog, ILogger logger)
    {
        AuditLog = auditLog;
        Logger   = logger;
    }

    public abstract string              Name           { get; }
    public abstract string              Description    { get; }

    public async Task<CommandResult> ExecuteAsync(Action<string>? report = null)
    {
        var startTime = DateTime.UtcNow;
    
        CommandResult result;

        try
        {
            // Pass the reporter through so derived commands can update the UI spinner.
            result = await ExecuteInternalAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Command {CommandName} failed with exception", Name);
            result = CommandResult.Fail($"Command failed: {ex.Message}", ex.ToString());
        }

        var duration = DateTime.UtcNow - startTime;
        result.Message += $" (Duration: {duration.TotalSeconds:F2}s)";
        // Logger.LogInformation("Command {CommandName} completed in {Duration}ms with status: {Success}"
        //                     , Name
        //                     , duration.TotalMilliseconds
        //                     , result.Success
        //                               ? "Success"
        //                               : "Failure");

        await LogAuditEntryAsync(result, CancellationToken.None);

        return result;
    }

    protected abstract Task<CommandResult> ExecuteInternalAsync(CancellationToken ct);
    
    protected virtual Task LogAuditEntryAsync(CommandResult result, CancellationToken ct)
    {
        var entry = new AuditEntry(DateTime.UtcNow.ToLocalTime()
                                 , Name
                                 , GetComponentName() ?? "N/A"
                                 , GetEnvironment()
                                 , System.Environment.UserName
                                 , System.Environment.MachineName
                                 , result.Message
                                 , result.Success
                                 , result.ErrorDetails);

        return AuditLog.LogAsync(entry, ct);
    }

    protected virtual string?      GetComponentName() => null;
    protected virtual Environment? GetEnvironment()   => null;

    public void Log<TState>( LogLevel                         logLevel
                           , EventId                          eventId
                           , TState                           state
                           , Exception?                       exception
                           , Func<TState, Exception?, string> formatter )
    {
        throw new NotImplementedException();
    }

    public bool IsEnabled( LogLevel logLevel ) => throw new NotImplementedException();

    public IDisposable? BeginScope<TState>( TState state ) where TState : notnull => throw new NotImplementedException();
}