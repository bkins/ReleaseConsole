using Microsoft.Extensions.Logging;
using ReleaseConsole.Core;
using ReleaseConsole.Services;
using ReleaseConsole.Services.Interfaces;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.Commands;

public sealed class RollbackCommand : CommandBaseCommand
{
    private readonly Component           _component;
    private readonly string              _targetVersion;
    private readonly IArtifactStorage    _artifactStorage;
    private readonly IPowerShellExecutor _psExecutor;
    private readonly string              _scriptsPath;

    public RollbackCommand(
        Component                component,
        string                   targetVersion,
        IArtifactStorage         artifactStorage,
        IPowerShellExecutor      psExecutor,
        IAuditLog                auditLog,
        ILogger<RollbackCommand> logger,
        string?                  scriptsPath = null)
            : base(auditLog, logger)
    {
        _component       = component;
        _targetVersion   = targetVersion;
        _artifactStorage = artifactStorage;
        _psExecutor      = psExecutor;
        _scriptsPath     = scriptsPath ?? GetDefaultScriptsPath();
    }

    public override string Name        => "rollback";
    public override string Description => $"Rollback {_component.Name} to version {_targetVersion}";

    protected override string      GetComponentName() => _component.Name;
    protected override Environment? GetEnvironment()   => Environment.Prod;

    protected override async Task<CommandResult> ExecuteInternalAsync(CancellationToken ct, Action<string>? report = null)
    {
        Logger.LogWarning("PRODUCTION ROLLBACK\n" 
                        + "Component: {Component}\n" 
                        + "Target Version: {Version}"
                        , _component.Name
                        , _targetVersion);

        Console.Write("Proceed with PRODUCTION ROLLBACK? Type 'ROLLBACK' to confirm: ");
        var response = Console.ReadLine()?.Trim();

        if (response != "ROLLBACK")
        {
            return CommandResult.Fail("Rollback cancelled by user");
        }

        // Get target artifact
        var artifact = await _artifactStorage.GetArtifactAsync(_component, Environment.Prod, _targetVersion, ct);
        if (artifact is null)
        {
            return CommandResult.Fail($"Version {_targetVersion} not found for {_component.Name}");
        }

        if (artifact.Metadata.BuiltFor != Environment.Prod)
        {
            return CommandResult.Fail($"Version {_targetVersion} is not a production artifact");
        }

        // Extract to temp
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        System.IO.Compression.ZipFile.ExtractToDirectory(artifact.Path, tempPath);

        try
        {
            // Execute deploy script (rollback uses same script as deploy)
            var scriptPath = Path.Combine(_scriptsPath, $"Deploy-{_component.Name}.ps1");
            if (!File.Exists(scriptPath))
            {
                return CommandResult.Fail($"Deploy script not found: {scriptPath}");
            }

            var parameters = new Dictionary<string, string>
                             {
                                     ["Environment"] = "Prod",
                                     ["SourcePath"]  = tempPath,
                                     ["Version"]     = _targetVersion
                             };

            Logger.LogInformation("Executing rollback deployment: {ScriptPath}", scriptPath);
            var psResult = await _psExecutor.ExecuteScriptAsync(scriptPath, _component.Type, parameters, ct);

            if (!psResult.Success)
            {
                Logger.LogError("Rollback failed with exit code {ExitCode}", psResult.ExitCode);
                return CommandResult.Fail("Rollback deployment failed", psResult.Error);
            }

            return CommandResult.Ok($"Rolled back {_component.Name} to version {_targetVersion}");
        }
        finally
        {
            if (Directory.Exists(tempPath))
                Directory.Delete(tempPath, true);
        }
    }

    private static string GetDefaultScriptsPath() =>
            Path.Combine(System.Environment.CurrentDirectory, "scripts");
}