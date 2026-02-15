using Microsoft.Extensions.Logging;
using ReleaseConsole.Core;
using ReleaseConsole.Services;
using ReleaseConsole.Services.Interfaces;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.Commands;

public sealed class PromoteCommand : CommandBaseCommand
{
    private readonly Component        _component;
    private readonly string           _justification;
    private readonly IArtifactStorage _artifactStorage;

    public PromoteCommand( Component               component
                         , string                  justification
                         , IArtifactStorage        artifactStorage
                         , IAuditLog               auditLog
                         , ILogger<PromoteCommand> logger )
            : base(auditLog
                 , logger)
    {
        if (string.IsNullOrWhiteSpace(justification))
            throw new ArgumentException("Justification required for production promotion"
                                      , nameof(justification));

        _component       = component;
        _justification   = justification;
        _artifactStorage = artifactStorage;
    }

    public override string Name        => "promote";
    public override string Description => $"Promote {_component.Name} from QA to Prod";

    protected override string       GetComponentName() => _component.Name;
    protected override Environment? GetEnvironment()   => Environment.Prod;

    protected override async Task<CommandResult> ExecuteInternalAsync(CancellationToken ct, Action<string>? report = null)
    {
        // Verify QA artifact exists
        var qaArtifact = await _artifactStorage.GetLatestArtifactAsync(_component
                                                                     , Environment.Qa
                                                                     , ct);
    
        if (qaArtifact is null)
        {
            return CommandResult.Fail($"No QA artifact found for {_component.Name}");
        }

        // Verify Prod artifact exists (should have been built alongside QA)
        var prodArtifact = await _artifactStorage.GetArtifactAsync(_component
                                                                   , Environment.Qa
                                                                 , qaArtifact.Version
                                                                 , ct);
    
        if (prodArtifact is null 
         || prodArtifact.Metadata.BuiltFor != Environment.Prod)
        {
            return CommandResult.Fail($"No Prod artifact found for version {qaArtifact.Version}. "
                                    + "This version may have been built before multi-environment builds were enabled.");
        }

        // Confirmation and audit logging...
        // (No rebuild needed - Prod artifact already exists!)

        return CommandResult.Ok($"Promoted {_component.Name} v{qaArtifact.Version} to Production\n"
                              + $"Justification: {_justification}");
    }

    protected override Task LogAuditEntryAsync(CommandResult result, CancellationToken ct)
    {
        var entry = new AuditEntry(DateTime.UtcNow.ToLocalTime()
                                 , Name
                                 , _component.Name
                                 , Environment.Prod
                                 , System.Environment.UserName
                                 , System.Environment.MachineName
                                 , $"{result.Message}\nJustification: {_justification}"
                                 , result.Success
                                 , result.ErrorDetails);

        return AuditLog.LogAsync(entry, ct);
    }
}