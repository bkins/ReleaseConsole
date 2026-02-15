using ReleaseConsole.Core;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.Services.Interfaces;

public interface IArtifactStorage
{
    Task<Artifact>                   SaveArtifactAsync(Component      component, string      version,     string            sourcePath, ArtifactMetadata metadata, CancellationToken ct = default);
    Task<Artifact?>                  GetLatestArtifactAsync(Component component, Environment environment, CancellationToken ct                                = default);
    Task<Artifact?>                  GetArtifactAsync(Component       component, Environment environment, string            version,     CancellationToken ct = default);
    Task<List<Artifact>> GetAllArtifactsAsync( Component         component
                                             , Environment       environment
                                             , CancellationToken ct = default );
    string                        GetArtifactsRootPath();
}