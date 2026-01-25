namespace ReleaseConsole.Core;

public sealed record Artifact( Component        Component
                             , string           Version
                             , string           Path
                             , ArtifactMetadata Metadata );