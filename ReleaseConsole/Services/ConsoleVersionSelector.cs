using CP.Client.Core.Avails;
using ReleaseConsole.Core;
using ReleaseConsole.Services.Interfaces;
using Spectre.Console;

namespace ReleaseConsole.Services;

public sealed class ConsoleVersionSelector : IVersionSelector
{
    public Task<string?> SelectVersionAsync( IReadOnlyList<Artifact> availableArtifacts,
                                             string                  promptMessage,
                                             CancellationToken       ct = default)
    {
        if (availableArtifacts.Any()
                              .Not())
        {
            return Task.FromResult<string?>(null);
        }

        var selection = AnsiConsole.Prompt(new SelectionPrompt<Artifact>().Title(promptMessage)
                                                                          .PageSize(10)
                                                                          .MoreChoicesText("[grey](Move up and down to see more versions)[/]")
                                                                          .AddChoices(availableArtifacts)
                                                                          .UseConverter(artifact => FormatArtifact(artifact)));

        return Task.FromResult<string?>(selection.Version);
    }
    
    private string FormatArtifact(Artifact artifact)
    {
        return $"v{artifact.Version} - {artifact.Metadata.BuildTimestamp:yyyy-MM-dd HH:mm} " 
             + $"({artifact.Metadata.GitCommitHash[..Math.Min(7, artifact.Metadata.GitCommitHash.Length)]})";
    }
}