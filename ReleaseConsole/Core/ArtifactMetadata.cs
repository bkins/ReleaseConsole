namespace ReleaseConsole.Core;

public sealed record ArtifactMetadata(string       ComponentName
                                     , string      Version
                                     , Environment BuiltFor
                                     , string      GitCommitHash
                                     , DateTime    BuildTimestamp
                                     , string      BuildMachine
                                     , string      ConsoleVersion );