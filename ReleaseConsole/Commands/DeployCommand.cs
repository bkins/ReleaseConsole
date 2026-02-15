using CP.Client.Core.Avails;
using Microsoft.Extensions.Logging;
using ReleaseConsole.Core;
using ReleaseConsole.Services;
using ReleaseConsole.Services.Interfaces;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.Commands;

public sealed class DeployCommand : CommandBaseCommand
{
    private readonly Component               _component;
    private readonly Environment             _environment;
    private readonly string?                 _targetVersion;
    private readonly IArtifactStorage        _artifactStorage;
    private readonly IPowerShellExecutor     _psExecutor;
    private readonly IDeploymentStateService _stateService;
    private readonly IVersionSelector?       _versionSelector;
    private readonly string                  _scriptsPath;
    private readonly bool                    _force;

    protected override ScriptOutputMode OutputMode => ScriptOutputMode.Normal;
    private bool OnlyNecessaryLogging => OutputMode is not ScriptOutputMode.Normal 
                                                   and not ScriptOutputMode.Verbose;
    
    public DeployCommand( Component               component
                        , Environment             environment
                        , IArtifactStorage        artifactStorage
                        , IPowerShellExecutor     psExecutor
                        , IDeploymentStateService stateService
                        , IAuditLog               auditLog
                        , ILogger<DeployCommand>  logger
                        , string?                 scriptsPath     = null
                        , bool                    force           = false 
                        , string?                 targetVersion   = null 
                        , IVersionSelector? versionSelector = null)
            : base(auditLog
                 , logger)
    {
        _component       = component;
        _environment     = environment;
        _targetVersion   = targetVersion;
        _artifactStorage = artifactStorage;
        _psExecutor      = psExecutor;
        _stateService    = stateService;
        _versionSelector = versionSelector;
        _scriptsPath     = scriptsPath ?? GetDefaultScriptsPath();
        _force           = force;

        _psExecutor.OutputMode = OutputMode;
    }

    public override string Name => "deploy";
    public override string Description => $"Deploy {_component.Name} to {_environment}" 
                                        + (_targetVersion != null 
                                                   ? $" (v{_targetVersion})" 
                                                   : " (latest)");

    protected override string GetComponentName() => _component.Name;
    protected override Environment? GetEnvironment() => _environment;

    protected override async Task<CommandResult> ExecuteInternalAsync(CancellationToken ct
                                                                    , Action<string>? report = null)
    {
        // Require confirmation for QA and Prod (unless --force is specified)
        if (_environment != Environment.Dev && !_force)
        {
            if (!await ConfirmDeploymentAsync())
            {
                return CommandResult.Fail("Deployment cancelled by user");
            }
        }
        else if (_environment != Environment.Dev && _force)
        {
            if(OnlyNecessaryLogging) Logger.LogWarning("Deploying to {Environment} with --force flag (skipping confirmation)", _environment);
        }

        // Get current deployment state
        var currentState = await _stateService.GetCurrentStateAsync(_component, _environment, ct);
        
        if (currentState is not null
            && OnlyNecessaryLogging )
        {
            Logger.LogInformation("Current deployment: {Component} v{Version} (deployed {When})"
                                , currentState.ComponentName
                                , currentState.Version
                                , currentState.DeployedAt.ToLocalTime());
        }

        // TODO: Create a method to determine the correct artifact version to deploy based:
        // on current state, target version, and available artifacts
        
        // Get latest compatible artifact
        // var artifact = await _artifactStorage.GetLatestArtifactAsync(_component, _environment, ct);
        
        var artifact = await SelectArtifactAsync(ct, report);
        
        if (artifact is null)
        {
            return CommandResult.Fail($"No artifact found for {_component.Name} targeting {_environment}");
        }
        
        if (OnlyNecessaryLogging) Console.WriteLine($"Artifact path: {artifact.Path}");
        
        if (File.Exists(artifact.Path).Not())
        {
            if(OnlyNecessaryLogging) Logger.LogError("Artifact file does not exist at expected path: {ArtifactPath}",
                                                     artifact.Path);

            return CommandResult.Fail($"Artifact not found on disk:\n{artifact.Path}\n" +
                                      $"Have you built the {_environment} artifact for {_component.Name}?");
        }

        if(OnlyNecessaryLogging) Logger.LogInformation("Deploying artifact: {Component} v{Version} to {Environment}"
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
            if (File.Exists(scriptPath).Not())
            {
                return CommandResult.Fail($"Deploy script not found: {scriptPath}");
            }

            if (OnlyNecessaryLogging) Console.WriteLine($"tempDeployPath: {tempDeployPath}");

            var parameters = BuildScriptParameters(artifact, tempDeployPath, currentState);

            if(OnlyNecessaryLogging) Logger.LogInformation("Executing deploy script: {ScriptPath}", scriptPath);
            var psResult = await _psExecutor.ExecuteScriptAsync(scriptPath, artifact.Component.Type, parameters, ct);

            if (psResult.Success.Not())
            {
                Logger.LogError("Deploy script failed with exit code {ExitCode}", psResult.ExitCode);
                return CommandResult.Fail("Deploy script failed", psResult.Error);
            }

            // Save a new deployment state
            var newState = CreateDeploymentState(artifact);
            await _stateService.SaveStateAsync(newState, ct);

            return CommandResult.Ok($"Deployed {_component.Name} v{artifact.Version} to {_environment}");
        }
        finally
        {
            // Cleanup temp directory
            if (Directory.Exists(tempDeployPath))
                Directory.Delete(tempDeployPath, true);
        }
    }

    private async Task<Artifact?> SelectArtifactAsync( CancellationToken ct
                                                     , Action<string>?   report = null )
    {
        // Case 1: Specific version requested via CLI
        if (_targetVersion?.HasValue() ?? false)
        {
            report?.Invoke($"Looking for version {_targetVersion}...");

            var artifact = await _artifactStorage.GetArtifactAsync(_component
                                                                 , _environment
                                                                 , _targetVersion
                                                                 , ct);
            if (artifact is null) return null;

            // Validate environment compatibility
            return ValidateArtifactEnvironment(artifact)
                           ? artifact
                           : null;
        }

        // Case 2: Interactive selection for Prod (if selector available)
        if (_environment     == Environment.Prod
         && _versionSelector != null)
        {
            report?.Invoke("Loading available versions...");

            var allArtifacts = await _artifactStorage.GetAllArtifactsAsync(_component
                                                                         , _environment
                                                                         , ct);
            var prodArtifacts = allArtifacts.Where(artifact => artifact.Metadata.BuiltFor == Environment.Prod)
                                            .OrderByDescending(artifact => artifact.Metadata.BuildTimestamp)
                                            .ToList();

            if (prodArtifacts.Count == 0)
            {
                Logger.LogWarning("No production artifacts available for selection");
                return null;
            }

            var selectedVersion = await _versionSelector.SelectVersionAsync(prodArtifacts
                                                                          , $"Select version to deploy to [red]PRODUCTION[/]:"
                                                                          , ct);

            if (selectedVersion?.HasValue() ?? false)
                return await _artifactStorage.GetArtifactAsync(_component
                                                             , _environment
                                                             , selectedVersion
                                                             , ct);
            Logger.LogWarning("Version selection cancelled");
            return null;

        }

        // Case 3: Deploy latest (Dev/QA default, or Prod fallback)
        report?.Invoke("Looking for latest artifact...");

        return await _artifactStorage.GetLatestArtifactAsync(_component
                                                           , _environment
                                                           , ct);
    }

    private bool ValidateArtifactEnvironment(Artifact artifact)
    {
        if (_environment               == Environment.Prod 
         && artifact.Metadata.BuiltFor != Environment.Prod)
        {
            Logger.LogError("Version {Version} was built for {BuiltFor}, not Prod. Use 'promote' first.",
                            artifact.Version,
                            artifact.Metadata.BuiltFor);
            return false;
        }

        if (_environment               != Environment.Prod &&
            artifact.Metadata.BuiltFor != _environment)
        {
            Logger.LogWarning("⚠️  Deploying {BuiltFor} artifact to {TargetEnv} environment"
                            , artifact.Metadata.BuiltFor
                            , _environment);
        }

        return true;
    } 
    
    private Dictionary<string, string> BuildScriptParameters( Artifact         artifact
                                                            , string           tempDeployPath
                                                            , DeploymentState? currentState )
    {
        var parameters = new Dictionary<string, string>
                         {
                                 ["Environment"] = _environment.ToString(), ["SourcePath"] = tempDeployPath, ["Version"] = artifact.Version
                               , ["Component"]   = _component.Name
                         };

        // Add current deployment info if it exists
        if (currentState is null) return parameters;
        
        parameters["CurrentVersion"] = currentState.Version;

        // Component-specific state
        if (_component.Type == ComponentType.Laa)
        {
            parameters["CurrentApkName"] = currentState.ApkName;
        }

        return parameters;
    }

    private DeploymentState CreateDeploymentState(Artifact artifact)
    {
        // Component-specific state creation
        return _component.Type switch
        {
            ComponentType.Laa => CreateLaaDeploymentState(artifact),
            ComponentType.Api => CreateApiDeploymentState(artifact),
            _ => throw new NotSupportedException($"Deployment state creation not implemented for {_component.Type}")
        };
    }

    private DeploymentState CreateLaaDeploymentState( Artifact artifact )
    {
        var appIdMap = new Dictionary<string, string>
                       {
                               ["Dev"] = "com.snikpoh.localaiassistant.dev", ["Qa"] = "com.snikpoh.localaiassistant.qa", ["Prod"] = "com.snikpoh.localaiassistant"
                       };

        var apkName = $"Laa-{artifact.Version}-{_environment.ToString().ToLower()}.apk";
        var appId   = appIdMap[_environment.ToString()];

        return new DeploymentState(ComponentName: _component.Name
                                 , Environment: _environment.ToString()
                                 , Version: artifact.Version
                                 , ApkName: apkName
                                 , AppId: appId
                                 , DeployedAt: DateTime.UtcNow
                                 , DeployedBy: System.Environment.UserName);
    }

    private DeploymentState CreateApiDeploymentState( Artifact artifact )
    {
        var deployPath     = $@"C:\CP\Deploy\Api\{_environment}";
        var executableName = $"CognitivePlatform.Api.{_environment}.exe";

        return new DeploymentState(ComponentName: _component.Name
                                 , Environment: _environment.ToString()
                                 , Version: artifact.Version
                                 , ApkName: executableName
                                 , AppId: deployPath           // Reusing ApkName field for executable name
                                 , DeployedAt: DateTime.UtcNow // Reusing AppId field for deploy path
                                 , DeployedBy: System.Environment.UserName);
    }

    private Task<bool> ConfirmDeploymentAsync()
    {
        // Logger.LogWarning("Deploying to {Environment} requires confirmation", _environment);
        
        Console.WriteLine();
        Console.WriteLine($"⚠️  WARNING: You are about to deploy {_component.Name} to {_environment}");
        Console.WriteLine();
        Console.Write("Type 'yes' to continue: ");
        
        var response = Console.ReadLine();
        Console.WriteLine();
        
        var confirmed = response?.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) ?? false;
        
        if (confirmed)
        {
            if (OnlyNecessaryLogging) Logger.LogInformation("✅ Deployment confirmed by user");
        }
        else
        {
            Logger.LogWarning("❌ Deployment cancelled by user (input: '{Response}')", response ?? "(empty)");
        }
        
        return Task.FromResult(confirmed);
    }

    private string GetDeployScriptPath() =>
        Path.Combine(_scriptsPath, $"{_component.Name}-Deploy.ps1");

    private static string GetDefaultScriptsPath() =>
        Path.Combine(System.Environment.CurrentDirectory, "scripts");
}