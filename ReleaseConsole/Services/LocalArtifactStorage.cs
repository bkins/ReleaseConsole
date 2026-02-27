using System.IO.Compression;
using System.Text.Json;
using CP.Client.Core.Avails;
using Microsoft.Extensions.Logging;
using ReleaseConsole.Core;
using ReleaseConsole.Services.Interfaces;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.Services;

public sealed class LocalArtifactStorage : IArtifactStorage
{
    private readonly string _artifactsRoot;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    
    private readonly ILogger<LocalArtifactStorage> _logger;
    
    public LocalArtifactStorage(ILogger<LocalArtifactStorage> logger
                              , string? artifactsRoot = null)
    {
        _logger = logger;
        _artifactsRoot = artifactsRoot ?? @"C:\CP\Artifacts\";
                         // Path.Combine(
                         //     System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                         //     "ReleaseConsole",
                         //     "artifacts"
                         // );

        Directory.CreateDirectory(_artifactsRoot);
    }

    public string GetArtifactsRootPath() => _artifactsRoot;

    public async Task<Artifact> SaveArtifactAsync( Component         component
                                                 , string            version
                                                 , string            sourcePath
                                                 , ArtifactMetadata  metadata
                                                 , CancellationToken ct = default )
    {
        var artifactDir = GetArtifactDirectory(component
                                             , version);
        Directory.CreateDirectory(artifactDir); // TODO: Redundant?

        var tempZipPath = Path.Combine(Path.GetTempPath()
                                     , $"{component.Name}-{version}-{Guid.NewGuid()}.zip");
        await ZipFile.CreateFromDirectoryAsync(artifactDir
                                             , tempZipPath
                                             , ct);
        
        // _logger.LogInformation("Moving zip file from {TempZipPath} to {ArtifactDir}"
        //                      , tempZipPath
        //                      , artifactDir);
        
        var finalZipPath = Path.Combine(artifactDir, $"{component.Name}.zip");
        File.Move(tempZipPath, finalZipPath, overwrite: true);
        
        var metadataPath = Path.Combine(artifactDir
                                      , "metadata.json");

        // Save metadata
        var json = JsonSerializer.Serialize(metadata
                                          , JsonOptions);
        await File.WriteAllTextAsync(metadataPath
                                   , json
                                   , ct);

        var finalMetadataPath = Path.Combine(sourcePath, Path.GetFileName(metadataPath));
        
         // _logger.LogInformation("Copying metadata file from {MetadataPath} to {FinalMetadataPath}"
         //                      , metadataPath
         //                      , finalMetadataPath);
        
        // This saves the most recent version in the root of the source directory for easy access by deployment scripts
        File.Copy(metadataPath, finalMetadataPath, overwrite: true);

        return new Artifact(component
                          , version
                          , finalZipPath
                          , metadata);
    }


    public async Task<IEnumerable<Artifact>> GetMostRecentArtifactsAsync(Component         component
                                                                       , Environment       environment
                                                                       , int               numberOfArtifacts 
                                                                       , CancellationToken ct = default )
    {
        var artifacts = await GetAllArtifactsAsync(component
                                                 , ct);

        return artifacts.OrderByDescending(artifact => artifact.Metadata.BuildTimestamp)
                        .Take(numberOfArtifacts);
    }
    
    public async Task<Artifact?> GetLatestArtifactAsync( Component         component
                                                       , CancellationToken ct = default )
    {
        var artifacts = await GetAllArtifactsAsync(component
                                                 , ct);

        if (artifacts.Count is 1)
        {
            return artifacts[0];
        }
        
        return artifacts.OrderByDescending(artifact => artifact.Metadata.BuildTimestamp)
                        .FirstOrDefault();
    }

    public async Task<Artifact?> GetArtifactAsync( Component         component
                                                 , string            version
                                                 , CancellationToken ct = default )
    {
        var artifactDir = GetArtifactDirectory(component
                                             , version);
        if (!Directory.Exists(artifactDir))
            return null;

        var metadataPath = Path.Combine(artifactDir
                                      , "metadata.json");
        if (!File.Exists(metadataPath))
            return null;

        var json = await File.ReadAllTextAsync(metadataPath
                                             , ct);
        var metadata = JsonSerializer.Deserialize<ArtifactMetadata>(json);

        if (metadata is null)
            return null;

        var zipPath = Path.Combine(artifactDir
                                 , $"{component.Name}.zip");

        //check if zip file exists
        return File.Exists(zipPath).Not()
                       ? null
                       : new Artifact(component
                                    , version
                                    , zipPath
                                    , metadata);

    }

    public async Task<List<Artifact>> GetAllArtifactsAsync( Component         component
                                                          , CancellationToken ct = default )
    {
        var componentDir = Path.Combine(_artifactsRoot, component.Name);
        if (!Directory.Exists(componentDir))
            return new List<Artifact>(Array.Empty<Artifact>());

        var artifacts   = new List<Artifact>();
        var versionDirs = Directory.GetDirectories(componentDir);

        foreach (var versionDir in versionDirs)
        {
            var version  = Path.GetFileName(versionDir);
            var artifact = await GetArtifactAsync(component, version, ct);
            if (artifact is not null)
                artifacts.Add(artifact);
        }

        return artifacts;
    }

    private string GetArtifactDirectory(Component component, string version) =>
            Path.Combine(_artifactsRoot, component.Name, version);
}