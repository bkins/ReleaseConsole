using System.IO.Compression;
using System.Text.Json;
using ReleaseConsole.Core;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.Services;

public sealed class LocalArtifactStorage : IArtifactStorage
{
    private readonly        string                _artifactsRoot;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public LocalArtifactStorage(string? artifactsRoot = null)
    {
        _artifactsRoot = artifactsRoot ?? Path.Combine(
                             System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                             "ReleaseConsole",
                             "artifacts"
                         );

        Directory.CreateDirectory(_artifactsRoot);
    }

    public string GetArtifactsRootPath() => _artifactsRoot;

    public async Task<Artifact> SaveArtifactAsync(
        Component         component,
        string            version,
        string            sourcePath,
        ArtifactMetadata  metadata,
        CancellationToken ct = default)
    {
        var artifactDir = GetArtifactDirectory(component, version);
        Directory.CreateDirectory(artifactDir);

        var zipPath      = Path.Combine(artifactDir, $"{component.Name}.zip");
        var metadataPath = Path.Combine(artifactDir, "metadata.json");

        // Create zip from source path
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        ZipFile.CreateFromDirectory(sourcePath, zipPath);

        // Save metadata
        var json = JsonSerializer.Serialize(metadata, JsonOptions);
        await File.WriteAllTextAsync(metadataPath, json, ct);

        return new Artifact(component, version, zipPath, metadata);
    }

    public async Task<Artifact?> GetLatestArtifactAsync(
        Component         component,
        Environment       environment,
        CancellationToken ct = default)
    {
        var artifacts = await GetAllArtifactsAsync(component, ct);
        
        return artifacts
               .Where(a => a.Metadata.BuiltFor == environment)
               .OrderByDescending(a => a.Metadata.BuildTimestamp)
               .FirstOrDefault();
    }

    public async Task<Artifact?> GetArtifactAsync(
        Component         component,
        string            version,
        CancellationToken ct = default)
    {
        var artifactDir = GetArtifactDirectory(component, version);
        if (!Directory.Exists(artifactDir))
            return null;

        var metadataPath = Path.Combine(artifactDir, "metadata.json");
        if (!File.Exists(metadataPath))
            return null;

        var json     = await File.ReadAllTextAsync(metadataPath, ct);
        var metadata = JsonSerializer.Deserialize<ArtifactMetadata>(json);

        if (metadata is null)
            return null;

        var zipPath = Path.Combine(artifactDir, $"{component.Name}.zip");
        return new Artifact(component, version, zipPath, metadata);
    }

    public async Task<IReadOnlyList<Artifact>> GetAllArtifactsAsync(
        Component         component,
        CancellationToken ct = default)
    {
        var componentDir = Path.Combine(_artifactsRoot, component.Name);
        if (!Directory.Exists(componentDir))
            return Array.Empty<Artifact>();

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