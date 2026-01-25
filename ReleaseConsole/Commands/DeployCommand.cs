using Microsoft.Extensions.Logging;
using ReleaseConsole.Core;
using ReleaseConsole.Services;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.Commands;

public sealed class DeployCommand : CommandBaseCommand
{
    private readonly Component           _component;
    private readonly Environment         _environment;
    private readonly IArtifactStorage    _artifactStorage;
    private readonly IPowerShellExecutor _psExecutor;
    private readonly string              _scriptsPath;

    public DeployCommand( Component              component
                        , Environment            environment
                        , IArtifactStorage       artifactStorage
                        , IPowerShellExecutor    psExecutor
                        , IAuditLog              auditLog
                        , ILogger<DeployCommand> logger
                        , string?                scriptsPath = null )
            : base(auditLog
                 , logger)
    {
        _component       = component;
        _environment     = environment;
        _artifactStorage = artifactStorage;
        _psExecutor      = psExecutor;
        _scriptsPath     = scriptsPath ?? GetDefaultScriptsPath();
    }

    public override string Name        => "deploy";
    public override string Description => $"Deploy {_component.Name} to {_environment}";

    protected override string       GetComponentName() => _component.Name;
    protected override Environment? GetEnvironment()   => _environment;

    protected override async Task<CommandResult> ExecuteInternalAsync(CancellationToken ct)
    {
        // Require confirmation for QA and Prod
        if (_environment != Environment.Dev)
        {
            Logger.LogWarning("Deploying to {Environment} requires confirmation", _environment);
            Console.Write($"Deploy {_component.Name} to {_environment}? (yes/no): ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();
            
            if (response != "yes")
            {
                return CommandResult.Fail("Deployment cancelled by user");
            }
        }

        // Get latest compatible artifact
        var artifact = await _artifactStorage.GetLatestArtifactAsync(_component, _environment, ct);
        if (artifact is null)
        {
            return CommandResult.Fail($"No artifact found for {_component.Name} targeting {_environment}");
        }

        Logger.LogInformation("Deploying artifact: {Component} v{Version} to {Environment}"
                            , artifact.Component.Name
                            , artifact.Version
                            , _environment);

        // Extract artifact to temp location
        var tempDeployPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        System.IO.Compression.ZipFile.ExtractToDirectory(artifact.Path, tempDeployPath);

        try
        {
            // Execute deployment script
            var scriptPath = GetDeployScriptPath();
            if ( ! File.Exists(scriptPath))
            {
                return CommandResult.Fail($"Deploy script not found: {scriptPath}");
            }

            var parameters = new Dictionary<string, string>
                             {
                                     ["Environment"] = _environment.ToString(),
                                     ["SourcePath"]  = tempDeployPath,
                                     ["Version"]     = artifact.Version
                             };

            Logger.LogInformation("Executing deploy script: {ScriptPath}", scriptPath);
            var psResult = await _psExecutor.ExecuteScriptAsync(scriptPath, parameters, ct);

            if (psResult.Success)
                return CommandResult.Ok($"Deployed {_component.Name} v{artifact.Version} to {_environment}");
            
            Logger.LogError("Deploy script failed with exit code {ExitCode}", psResult.ExitCode);
            return CommandResult.Fail("Deploy script failed", psResult.Error);

        }
        finally
        {
            // Cleanup temp directory
            if (Directory.Exists(tempDeployPath))
                Directory.Delete(tempDeployPath, true);
        }
    }

    private string GetDeployScriptPath() =>
            Path.Combine(_scriptsPath, $"Deploy-{_component.Name}.ps1");

    private static string GetDefaultScriptsPath() =>
            Path.Combine(System.Environment.CurrentDirectory, "scripts");
}