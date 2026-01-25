using Microsoft.Extensions.Logging;
using ReleaseConsole.Core;
using ReleaseConsole.Services;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.Commands;

public sealed class BuildCommand : CommandBaseCommand
{
    private readonly Component           _component;
    private readonly Environment         _environment;
    private readonly IArtifactStorage    _artifactStorage;
    private readonly IPowerShellExecutor _psExecutor;
    private readonly string              _scriptsPath;
    private readonly string?             _deployPath;

    public BuildCommand( Component             component
                        , Environment          environment
                        , IArtifactStorage      artifactStorage
                        , IPowerShellExecutor   psExecutor
                        , IAuditLog             auditLog
                        , ILogger<BuildCommand> logger
                        , string?               scriptsPath = null
        , string?                 deployPath = null)
            : base(auditLog, logger)
    {
        if (environment == Environment.Prod)
            throw new InvalidOperationException("Cannot build directly for Prod. Use promote instead.");

        _component       = component;
        _environment     = environment;
        _artifactStorage = artifactStorage;
        _psExecutor      = psExecutor;
        _scriptsPath     = scriptsPath ?? GetDefaultScriptsPath();
        
        var translatedComponentName = _component.Name == nameof(ComponentType.Laa) ? "MAUI" : _component.Name;
        _deployPath = deployPath ?? GetDefaultDeployPath(translatedComponentName, _environment.ToString());
    }

    public override string Name        => "build";
    public override string Description => $"Build {_component.Name} for {_environment}";

    protected override string       GetComponentName() => _component.Name;
    protected override Environment? GetEnvironment()   => _environment;

    protected override async Task<CommandResult> ExecuteInternalAsync(CancellationToken ct)
    {
        Logger.LogInformation("Building {Component} for {Environment}", _component.Name, _environment);

        // Generate version
        var version = GenerateVersion();
        Logger.LogInformation("Generated version: {Version}", version);

        // Execute build script
        var scriptPath = GetBuildScriptPath();
        Logger.LogInformation("Executing build script: {ScriptPath}", scriptPath);
        
        if (!File.Exists(scriptPath))
        {
            return CommandResult.Fail($"Build script not found: {scriptPath}");
        }

        var parameters = new Dictionary<string, string>
                         {
                                 ["Environment"] = _environment.ToString(),
                                 ["Version"]     = version
                         };

        Logger.LogInformation("Executing build script: {ScriptPath}", scriptPath);
        var psResult = await _psExecutor.ExecuteScriptAsync(scriptPath, parameters, ct);

        if ( ! psResult.Success)
        {
            Logger.LogError("Build script failed with exit code {ExitCode}", psResult.ExitCode);
            return CommandResult.Fail("Build script failed", psResult.Error);
        }

        // Determine output path (your dotnet publish output location)
        var publishOutputPath = _deployPath; //GetPublishOutputPath();

        if ( ! Directory.Exists(publishOutputPath))
        {
            return CommandResult.Fail($"Build output not found at: {publishOutputPath}");
        }

        // Create artifact metadata
        var metadata = new ArtifactMetadata(_component.Name
                                          , version
                                          , _environment
                                          , GetGitCommitHash()
                                          , DateTime.UtcNow
                                          , System.Environment.MachineName
                                          , ConsoleVersion.Version);

        // Save artifact
        var artifact = await _artifactStorage.SaveArtifactAsync(_component
                                                              , version
                                                              , publishOutputPath
                                                              , metadata
                                                              , ct);

        Logger.LogInformation("Artifact saved: {Path}", artifact.Path);

        return CommandResult.Ok($"Built {_component.Name} v{version} for {_environment}\n" +
                                $"Artifact: {artifact.Path}"
        );
    }

    private string GetBuildScriptPath() =>
            Path.Combine(_scriptsPath, $"{_component.Name}-Build.ps1");

    private string GetPublishOutputPath() =>
            Path.Combine(_scriptsPath, "..", "publish", _component.Name, _environment.ToString());

    private static string GenerateVersion()
    {
        var now = DateTime.UtcNow;
        return $"1.0.{now:yyyyMMdd}.{now:HHmmss}";
    }

    private static string GetGitCommitHash()
    {
        try
        {
            var process = new System.Diagnostics.Process
                          {
                                  StartInfo = new System.Diagnostics.ProcessStartInfo
                                              {
                                                      FileName               = "git"
                                                    , Arguments              = "rev-parse HEAD"
                                                    , RedirectStandardOutput = true
                                                    , UseShellExecute        = false
                                                    , CreateNoWindow         = true
                                              }
                          };

            process.Start();
            var hash = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            return string.IsNullOrEmpty(hash) ? "unknown" : hash;
        }
        catch
        {
            return "unknown";
        }
    }

    private static string GetDefaultScriptsPath() => Path.Combine(System.Environment.CurrentDirectory
                                                                 , "scripts");
    private static string? GetDefaultDeployPath(string componentName, string env) => @$"C:\Deploy\CP\{componentName}\{env}";
}