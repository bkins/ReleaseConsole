using System.IO.Compression;
using System.Text;
using CP.Client.Core.Avails;
using Microsoft.Extensions.Logging;
using ReleaseConsole.Core;
using ReleaseConsole.Services;
using ReleaseConsole.Services.Interfaces;
using Spectre.Console;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.Commands;

public sealed class BuildCommand : CommandBaseCommand
{
    private readonly Component           _component;
    public           Environment         Environment;
    private readonly IArtifactStorage    _artifactStorage;
    private readonly IPowerShellExecutor _psExecutor;
    private readonly string              _scriptsPath;
    private readonly string              _newLine = System.Environment.NewLine;

    protected override ScriptOutputMode OutputMode => ScriptOutputMode.Normal;
    
    private bool OnlyNecessaryLogging => OutputMode is not ScriptOutputMode.Normal 
                                                    and not ScriptOutputMode.Verbose;

    public BuildCommand( Component             component
                       , Environment           environment
                       , IArtifactStorage      artifactStorage
                       , IPowerShellExecutor   psExecutor
                       , IAuditLog             auditLog
                       , ILogger<BuildCommand> logger
                       , string?               scriptsPath = null )
            : base(auditLog
                 , logger)
    {
        // Only restrict Prod builds for non-API components
        // API builds universal artifacts that work in any environment
        if (component.Type != ComponentType.Api && environment == Environment.Prod)
            throw new InvalidOperationException($"Cannot build {component.Name} directly for Prod. Build for QA and deploy to Prod.");

        _component       = component;
        Environment      = environment;
        _artifactStorage = artifactStorage;
        _psExecutor      = psExecutor;
        _scriptsPath     = scriptsPath ?? GetDefaultScriptsPath();
    }

    public override string Name        => "build";
    public override string Description => $"Build {_component.Name} for {Environment}";

    protected override string       GetComponentName() => _component.Name;
    protected override Environment? GetEnvironment()   => Environment;

    protected override async Task<CommandResult> ExecuteInternalAsync( CancellationToken ct)
    {
        var version = GenerateVersion();    
        
        // Execute build script
        var scriptPath = GetBuildScriptPath();
        
        if (File.Exists(scriptPath)
                .Not())
        {
            return CommandResult.Fail($"Build script not found: {scriptPath}");
        }

        Dictionary<string, string> parameters;
        PowerShellResult           psResult = new PowerShellResult(false, "Unknown Error", "Unknown Error Occurred!", -1);
        
        switch (_component.Type)
        {
            case ComponentType.Api:
                parameters = new Dictionary<string, string>
                             {
                                     ["Environment"] = Environment.ToString()
                                   , ["Version"]     = version!
                                   , ["OutputPath"] = Path.Combine(_artifactStorage.GetArtifactsRootPath()
                                                                 , _component.Name
                                                                 , version)
                             };

                psResult = await _psExecutor.ExecuteScriptAsync(scriptPath
                                                              , _component.Type
                                                              , parameters
                                                              , ct);
                break;
            case ComponentType.Laa:
            {
                foreach (var environment in new List<Environment> { Environment.Dev, Environment.Qa, Environment.Prod })
                {
                    parameters = new Dictionary<string, string>
                                 {
                                         ["Environment"] = environment.ToString()
                                       , ["Version"]     = version!
                                       , ["ArtifactsPath"] = Path.Combine(_artifactStorage.GetArtifactsRootPath()
                                                                        , _component.Name
                                                                        , version)
                                 };

                    psResult = await _psExecutor.ExecuteScriptAsync(scriptPath
                                                                  , _component.Type
                                                                  , parameters
                                                                  , ct);
                }

                break;
            }
            default:
                psResult = await _psExecutor.ExecuteScriptAsync(scriptPath
                                                              , _component.Type
                                                              , new Dictionary<string, string>()
                                                              , ct);
                break;
        }
        
        if (psResult.Success.Not())
        {
            Logger.LogError("Build script failed with exit code {ExitCode}\nDetails:\n{Details}"
                          , psResult.ExitCode
                          , psResult.Output);
            return CommandResult.Fail("Build script failed"
                                    , psResult.Error);
        }

        if (_component.Type == ComponentType.AllNuget)
        {
            return CommandResult.Ok(psResult.Output);
        }
        // Determine the output path (your dotnet publish output location)
        var publishOutputPath = @$"C:\CP\Artifacts\{_component.Name}"; //GetPublishOutputPath();
        if (Directory.Exists(publishOutputPath).Not())
        {
            return CommandResult.Fail($"Build output not found at: {publishOutputPath}");
        }

        // Create artifact metadata
        // For API: BuiltFor is just the environment label used during build (but artifact is universal)
        // For MAUI: BuiltFor is the actual target environment
        var metadata = new ArtifactMetadata(_component.Name
                                          , version
                                          , Environment
                                          , // This is now just metadata, not a deployment restriction
                                            GetGitCommitHash()
                                          , DateTime.UtcNow
                                          , System.Environment.MachineName
                                          , ConsoleVersion.Version);

        // Save artifact
        var artifact = await _artifactStorage.SaveArtifactAsync(_component
                                                              , version!
                                                              , publishOutputPath
                                                              , metadata
                                                              , ct);

        Logger.LogInformation("Artifact saved: {Path}"
                            , artifact.Path);

        return CommandResult.Ok($"Built {_component.Name} v{version} for {Environment}\n" + $"Artifact: {artifact.Path}");
    }


    public delegate void StatusReporter(string message);

    /// <summary>
    /// For when a component type requires building all environments at once (e.g. MAUI or NuGets apps).
    /// </summary>
    /// <param name="component"></param>
    /// <param name="ct"></param>
    /// <param name="report"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private async Task<CommandResult> BuildAllEnvironmentsAsync( Component     component
                                                                   , CancellationToken ct
                                                                   , Action<string>?   report = null )

    {
        if (component.Type == ComponentType.Laa)
        {
            return await BuildLaa(ct
                                , report);
        }

        return await BuildNuGetPackages(component
                                      , ct);
    }

    private async Task<CommandResult> BuildNuGetPackages( Component     component
                                                        , CancellationToken ct )
    {
        var scriptPath = Path.Combine(_scriptsPath, $"publish-local-nuget.ps1");

        var parameters = new Dictionary<string, string>
                         {
                                 ["Target"] = component.TargetName
                         };

        var psResult = await _psExecutor.ExecuteScriptAsync(scriptPath
                                                          , component.Type
                                                          , parameters
                                                          , ct);
        if (OutputMode == ScriptOutputMode.Verbose)
        {
            // DEBUG: Let's see what we actually captured
            Console.WriteLine("=== DEBUG OUTPUT ===");
            Console.WriteLine($"Success: {psResult.Success}");
            Console.WriteLine($"Exit Code: {psResult.ExitCode}");
            Console.WriteLine($"Output Length: {psResult.Output?.Length ?? 0}");
            Console.WriteLine($"Output: [{psResult.Output}]");
            Console.WriteLine($"Error: [{psResult.Error}]");
            Console.WriteLine("=== END DEBUG ===");
    
        }
        
        if (psResult.Success.Not())
        {
            return new CommandResult(false, psResult.Output, psResult.Error);
        }

        // Extract only the summary lines (those starting with ✅)
        var summaryLines = psResult.Output
                                   .Split(new[] {System.Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                                   //.Where(line => line.Contains("✅"))
                                   .ToList();

        var summary = string.Join(System.Environment.NewLine, summaryLines);

        return new CommandResult(true, summary);
    }
    
    private async Task<CommandResult> BuildLaa( CancellationToken ct
                                              , Action<string>?   report )
    {

        var version = GenerateVersion();
        var artifactRootPath = Path.Combine(_artifactStorage.GetArtifactsRootPath()
                                          , _component.Name
                                          , version);

        Directory.CreateDirectory(artifactRootPath);

        var environmentResults = new List<string>();

        var environments = new[] { Environment.Dev, Environment.Qa, Environment.Prod };

        for (int i = 0; i < environments.Length; i++)
        {
            var env = environments[i];

            report?.Invoke($" for {env}...");
            var result = await BuildMauiEnvironment(env
                                                  , version
                                                  , artifactRootPath
                                                  , ct);


            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"Build failed for {env}: {result.Error}");
            }

            environmentResults.Add(result.Summary);
        }

        // Create metadata.json at root artifact level
        var metadata = new ArtifactMetadata(_component.Name
                                          , version
                                          , Environment.All
                                          , GetGitCommitHash()
                                          , DateTime.UtcNow.ToLocalTime()
                                          , System.Environment.MachineName
                                          , ConsoleVersion.Version);

        var metadataPath = Path.Combine(artifactRootPath
                                      , "metadata.json");
        var json = System.Text.Json.JsonSerializer.Serialize(metadata
                                                           , new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(metadataPath
                                   , json
                                   , ct);

        if (OnlyNecessaryLogging)
            Logger.LogInformation("Metadata saved: {Path}"
                                , metadataPath);

        // Build success summary
        var summary = new StringBuilder();
        summary.AppendLine($"Built {_component.Name} v{version} for ALL environments");
        summary.AppendLine(string.Empty);
            
        foreach (var line in environmentResults) summary.AppendLine(line);
        summary.AppendLine(string.Empty);
        summary.AppendLine($"Artifacts: {artifactRootPath}");

        return new CommandResult(true
                               , summary.ToString());
    }

    private async Task<(bool Success, string Summary, string Error)> 
            BuildMauiEnvironment(Environment env, string version, string artifactRootPath, CancellationToken ct)
    {
        var envArtifactPath = Path.Combine(artifactRootPath, env.ToString());
        Directory.CreateDirectory(envArtifactPath);

        var scriptPath = GetBuildScriptPath();

        var parameters = new Dictionary<string, string>
                         {
                                 ["Environment"]   = env.ToString().ToUpperInvariant(),
                                 ["Version"]       = version,
                                 ["ArtifactsPath"] = envArtifactPath
                         };

        var psResult = await _psExecutor.ExecuteScriptAsync(scriptPath, ComponentType.Laa, parameters, ct);

        if (psResult.Success.Not())
        {
            return (false, "", psResult.Error);
        }

        var apkPath = Directory.GetFiles(envArtifactPath, "*.apk").FirstOrDefault();
        if (apkPath is null)
        {
            return (false, "", $"No APK found for {env}");
        }

        var zipPath = Path.Combine(artifactRootPath
                                 , $"{_component.Name}-{env}.zip");

        if (File.Exists(zipPath))
            File.Delete(zipPath);

        ZipFile.CreateFromDirectory(envArtifactPath, zipPath);

        return (true, $"✅  {env}: {Path.GetFileName(apkPath)} → {zipPath}", "");
    }

    
    private async Task<CommandResult> BuildSingleEnvironmentAsync( Environment       environment
                                                                 , string            version
                                                                 , CancellationToken ct
                                                                 , Action<string>?   report = null)
    {
        // Report status if a reporter was supplied (spinner will update)
        report?.Invoke($"Building {_component.Name} for {environment}...");

        if(OnlyNecessaryLogging) Logger.LogInformation("Building {Component} for {Environment}"
                                                     , _component.Name
                                                     , environment);
        // Create artifact directory structure
        // Example: C:\CP\Artifacts\API\1.0.2223.947\Dev
        var artifactPath = Path.Combine(_artifactStorage.GetArtifactsRootPath()
                                      , _component.Name
                                      , version
                                      , environment.ToString());

        Directory.CreateDirectory(artifactPath);
        if(OnlyNecessaryLogging) Logger.LogInformation("Created artifact directory: {Path}", artifactPath);

        // Execute build script
        var scriptPath = GetBuildScriptPath();
        if(OnlyNecessaryLogging) Logger.LogInformation("Executing build script: {ScriptPath}", scriptPath);
        
        if (!File.Exists(scriptPath))
        {
            return CommandResult.Fail($"Build script not found: {scriptPath}");
        }

        var parameters = new Dictionary<string, string>
                         {
                                 ["Environment"] = environment.ToString().ToUpperInvariant(), // DEV, QA
                                 ["Version"]     = version,
                                 ["OutputPath"]  = artifactPath // Tell script where to publish
                         };

        var psResult = await _psExecutor.ExecuteScriptAsync(scriptPath, _component.Type, parameters, ct);

        if (!psResult.Success)
        {
            Logger.LogError("Build script failed with exit code {ExitCode}", psResult.ExitCode);
            return CommandResult.Fail("Build script failed", psResult.Error);
        }

        // Verify build output exists
        if (!Directory.Exists(artifactPath) || !Directory.GetFiles(artifactPath).Any())
        {
            return CommandResult.Fail($"Build output not found or empty at: {artifactPath}");
        }

        // Create zip artifact
        var zipPath = Path.Combine(
            _artifactStorage.GetArtifactsRootPath(),
            _component.Name,
            version,
            $"{_component.Name}-{environment}.zip");

        if (File.Exists(zipPath))
            File.Delete(zipPath);

        System.IO.Compression.ZipFile.CreateFromDirectory(artifactPath, zipPath);
        
        if(OnlyNecessaryLogging) Logger.LogInformation("Created artifact zip: {ZipPath}", zipPath);

        // Create artifact metadata
        var metadata = new ArtifactMetadata(_component.Name
                                          , version
                                          , environment
                                          , GetGitCommitHash()
                                          , DateTime.UtcNow.ToLocalTime()
                                          , System.Environment.MachineName
                                          , ConsoleVersion.Version);

        var metadataPath = Path.Combine(_artifactStorage.GetArtifactsRootPath()
                                      , _component.Name
                                      , version
                                      , "metadata.json");

        var json = System.Text.Json.JsonSerializer.Serialize(metadata
                                                           , new System.Text.Json.JsonSerializerOptions 
                                                             { WriteIndented = true });
        
        await File.WriteAllTextAsync(metadataPath, json, ct);

        if(OnlyNecessaryLogging) Logger.LogInformation("Metadata saved: {Path}", metadataPath);

        return CommandResult.Ok($"Built {_component.Name} v{version} for {environment}{_newLine}" 
                              + $"Artifact: {zipPath}");
    }
    
    private string GetBuildScriptPath()
    {
        var scriptName = _component.Type switch
                         {
                                 ComponentType.AllNuget => "publish-local-nuget.ps1"
                                , _                     => $"{_component.Name}-Build.ps1"
                         };
        return Path.Combine(_scriptsPath, scriptName);
    }

    private static readonly object _versionLock = new();

    public static string GenerateVersion(string major = "1", string minor = "0")
    {
        lock (_versionLock)
        {
            var path = @"C:\Users\benho\source\repos\CP\CP.Workbench\version.json";
            if (!System.IO.File.Exists(path))
            {
                return $"{major}.{minor}.0.1";
            }

            try
            {
                var json = System.IO.File.ReadAllText(path);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                int majorVal = root.GetProperty("Major").GetInt32();
                int minorVal = root.GetProperty("Minor").GetInt32();
                int patchVal = root.GetProperty("Patch").GetInt32();
                int buildVal = root.GetProperty("Build").GetInt32();

                buildVal++;

                // Write back updated build number
                var updatedJson = $"{{\n  \"Major\": {majorVal},\n  \"Minor\": {minorVal},\n  \"Patch\": {patchVal},\n  \"Build\": {buildVal}\n}}";
                System.IO.File.WriteAllText(path, updatedJson);

                return $"{majorVal}.{minorVal}.{patchVal}.{buildVal}";
            }
            catch
            {
                return $"{major}.{minor}.0.1";
            }
        }
    }

    private static string GetGitCommitHash()
    {
        try
        {
            var process = new System.Diagnostics.Process
                          {
                                  StartInfo = new System.Diagnostics.ProcessStartInfo
                                              {
                                                      FileName               = "git",
                                                      Arguments              = "rev-parse HEAD",
                                                      RedirectStandardOutput = true,
                                                      UseShellExecute        = false,
                                                      CreateNoWindow         = true
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
}
