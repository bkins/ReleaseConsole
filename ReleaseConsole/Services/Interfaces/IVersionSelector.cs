using ReleaseConsole.Core;

namespace ReleaseConsole.Services.Interfaces;

public interface IVersionSelector
{
    Task<string?> SelectVersionAsync(
        IReadOnlyList<Artifact> availableArtifacts,
        string                  promptMessage,
        CancellationToken       ct = default);
}