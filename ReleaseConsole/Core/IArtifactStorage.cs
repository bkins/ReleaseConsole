using ReleaseConsole.Core;
using Environment = ReleaseConsole.Core.Environment;

public interface IArtifactStorage
{
    Task<Artifact> SaveArtifactAsync(Component component, string version, string sourcePath, ArtifactMetadata metadata, CancellationToken ct = default);
    Task<Artifact?> GetLatestArtifactAsync(Component component, Environment environment, CancellationToken ct = default);
    Task<Artifact?> GetArtifactAsync(Component component, string version, CancellationToken ct = default);
    Task<IReadOnlyList<Artifact>> GetAllArtifactsAsync(Component component, CancellationToken ct = default);
    string GetArtifactsRootPath();
}