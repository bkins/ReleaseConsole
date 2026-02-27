using ReleaseConsole.Core;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.Services.Interfaces;

public interface IArtifactStorage
{
    Task<Artifact> SaveArtifactAsync( Component         component
                                    , string            version
                                    , string            sourcePath
                                    , ArtifactMetadata  metadata
                                    , CancellationToken ct = default );

    Task<Artifact?> GetLatestArtifactAsync( Component         component
                                          , CancellationToken ct = default );

    Task<Artifact?> GetArtifactAsync( Component         component
                                    , string            version
                                    , CancellationToken ct = default );

    Task<List<Artifact>> GetAllArtifactsAsync( Component         component
                                             , CancellationToken ct = default );

    Task<IEnumerable<Artifact>> GetMostRecentArtifactsAsync( Component         component
                                                           , Environment       environment
                                                           , int               numberOfArtifacts 
                                                           , CancellationToken ct = default);

    string GetArtifactsRootPath();
}