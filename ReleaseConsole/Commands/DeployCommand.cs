using Microsoft.Extensions.Logging;
using ReleaseConsole.Core;
using ReleaseConsole.Services;
using ReleaseConsole.Services.Interfaces;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.Commands;

public sealed class DeployCommand : CommandBaseCommand
{
    private readonly Component?          _component;
    private readonly Environment         _environment;
    private readonly string?             _targetVersion;
    private readonly IArtifactStorage    _artifactStorage;
    private readonly IPowerShellExecutor _psExecutor;
    private readonly string              _scriptsPath;

    public DeployCommand( Component              component
                        , Environment            environment
                        , IArtifactStorage       artifactStorage
                        , IPowerShellExecutor    psExecutor
                        , IAuditLog              auditLog
                        , ILogger<DeployCommand> logger
                        , string?                scriptsPath   = null
                        , string?                targetVersion = null )
            : base(auditLog
                 , logger)
    {
        _component       = component;
        _environment     = environment;
        _targetVersion   = targetVersion;
        _artifactStorage = artifactStorage;
        _psExecutor      = psExecutor;
        _scriptsPath     = scriptsPath ?? GetDefaultScriptsPath();
    }

    public override string Name => "deploy";
    public override string Description => $"Deploy {_component.Name} to {_environment}";

    protected override string GetComponentName() => _component.Name;
    protected override Environment? GetEnvironment() => _environment;

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

        // Get artifact (either specific version or latest)
        Artifact? artifact;
        
        if (!string.IsNullOrWhiteSpace(_targetVersion))
        {
            // Deploy specific version
             Logger.LogInformation("Retrieving specific version: {Version}", _targetVersion);
            artifact = await _artifactStorage.GetArtifactAsync(_component, _targetVersion, ct);
            
            if (artifact is null)
            {
                return CommandResult.Fail($"Artifact not found: {_component.Name} v{_targetVersion}");
            }
        }
        else
        {
            // Deploy latest artifact
            Logger.LogInformation("Retrieving latest artifact");
            
            if (_component.Type == ComponentType.Api)
            {
                // For API: Get latest artifact regardless of environment tag
                var allArtifacts = await _artifactStorage.GetAllArtifactsAsync(_component, ct);
                artifact = allArtifacts
                    .OrderByDescending(a => a.Metadata.BuildTimestamp)
                    .FirstOrDefault();
            }
            else
            {
                // For MAUI: Get latest artifact for this specific environment
                artifact = await _artifactStorage.GetLatestArtifactAsync(_component, ct);
            }
            
            if (artifact is null)
            {
                return CommandResult.Fail($"No artifact found for {_component.Name}");
            }
        }

        // SAFETY CHECK: For Prod deployments, verify it was deployed to QA first
        if (_environment == Environment.Prod)
        {
            var wasDeployedToQA = await WasDeployedToEnvironmentAsync(artifact.Version
                                                                    , Environment.Qa
                                                                    , ct);
            
            if (!wasDeployedToQA)
            {
                return CommandResult.Fail(
                    $"Version {artifact.Version} has not been deployed to QA.\n" +
                    "All Prod deployments must be tested in QA first.\n" +
                    "Deploy to QA, verify, then deploy to Prod.");
            }
            
            Logger.LogInformation("✓ Verified: Version {Version} was previously deployed to QA",
                                  artifact.Version);
        }

        Logger.LogInformation("Deploying artifact: {Component} v{Version} to {Environment}",
                              artifact.Component.Name,
                              artifact.Version,
                              _environment);

        // Extract artifact to temp location
        var tempDeployPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        System.IO.Compression.ZipFile.ExtractToDirectory(artifact.Path, tempDeployPath);

        try
        {
            // Execute deployment script
            var scriptPath = GetDeployScriptPath();
            if (!File.Exists(scriptPath))
            {
                return CommandResult.Fail($"Deploy script not found: {scriptPath}");
            }

            var parameters = new Dictionary<string, string>
            {
                ["Environment"] = _environment.ToString(),
                ["SourcePath"] = tempDeployPath,
                ["Version"] = artifact.Version
            };

            Logger.LogInformation("Executing deploy script: {ScriptPath}", scriptPath);
            var psResult = await _psExecutor.ExecuteScriptAsync(scriptPath, _component.Type, parameters, ct);

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

    private async Task<bool> WasDeployedToEnvironmentAsync( string            version
                                                          , Environment       environment
                                                          , CancellationToken ct )
    {
        // Check audit log for successful deployment to that environment
        var recentEntries = await AuditLog.GetRecentEntriesAsync(1000
                                                               , ct);

        return recentEntries.Any(entry => entry.Command     == "deploy"
                                       && entry.Component   == _component.Name
                                       && entry.Environment == environment
                                       && entry.Success
                                       && entry.Details.Contains(version));
    }

    private string GetDeployScriptPath() =>
        Path.Combine(_scriptsPath, $"{_component.Name}-Deploy.ps1");

    private static string GetDefaultScriptsPath() =>
        Path.Combine(System.Environment.CurrentDirectory, "scripts");
}