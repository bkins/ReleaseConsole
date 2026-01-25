using Microsoft.Extensions.Logging;
using ReleaseConsole.Core;
using ReleaseConsole.Services;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.Commands;

public sealed class PromoteCommand : CommandBaseCommand
{
    private readonly Component        _component;
    private readonly string           _justification;
    private readonly IArtifactStorage _artifactStorage;

    public PromoteCommand(
        Component               component,
        string                  justification,
        IArtifactStorage        artifactStorage,
        IAuditLog               auditLog,
        ILogger<PromoteCommand> logger)
            : base(auditLog, logger)
    {
        if (string.IsNullOrWhiteSpace(justification))
            throw new ArgumentException("Justification required for production promotion", nameof(justification));

        _component       = component;
        _justification   = justification;
        _artifactStorage = artifactStorage;
    }

    public override string Name        => "promote";
    public override string Description => $"Promote {_component.Name} from QA to Prod";

    protected override string       GetComponentName() => _component.Name;
    protected override Environment? GetEnvironment()   => Environment.Prod;

    protected override async Task<CommandResult> ExecuteInternalAsync(CancellationToken ct)
    {
        // Get QA artifact
        var qaArtifact = await _artifactStorage.GetLatestArtifactAsync(_component, Environment.Qa, ct);
        if (qaArtifact is null)
        {
            return CommandResult.Fail($"No QA artifact found for {_component.Name}");
        }

        Logger.LogWarning(
            "PRODUCTION PROMOTION\n"   +
            "Component: {Component}\n" +
            "Version: {Version}\n"     +
            "Justification: {Justification}",
            _component.Name,
            qaArtifact.Version,
            _justification
        );

        Console.Write("Proceed with PRODUCTION promotion? Type 'PROMOTE' to confirm: ");
        var response = Console.ReadLine()?.Trim();

        if (response != "PROMOTE")
        {
            return CommandResult.Fail("Production promotion cancelled by user");
        }

        // Create new metadata for Prod
        var prodMetadata = qaArtifact.Metadata with
                           {
                                   BuiltFor = Environment.Prod
                           };

        // Extract QA artifact
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        System.IO.Compression.ZipFile.ExtractToDirectory(qaArtifact.Path, tempPath);

        try
        {
            // Save as Prod artifact
            var prodArtifact = await _artifactStorage.SaveArtifactAsync(
                                   _component,
                                   qaArtifact.Version,
                                   tempPath,
                                   prodMetadata,
                                   ct
                               );

            Logger.LogInformation("Promoted to Prod: {Path}", prodArtifact.Path);

            return CommandResult.Ok(
                $"Promoted {_component.Name} v{qaArtifact.Version} to Production\n" +
                $"Justification: {_justification}"
            );
        }
        finally
        {
            if (Directory.Exists(tempPath))
                Directory.Delete(tempPath, true);
        }
    }

    protected override Task LogAuditEntryAsync(CommandResult result, CancellationToken ct)
    {
        var entry = new AuditEntry(
            DateTime.UtcNow,
            Name,
            _component.Name,
            Environment.Prod,
            System.Environment.UserName,
            System.Environment.MachineName,
            $"{result.Message}\nJustification: {_justification}",
            result.Success,
            result.ErrorDetails
        );

        return AuditLog.LogAsync(entry, ct);
    }
}