namespace ReleaseConsole.Core;

/// <summary>
/// Represents the state of a deployed component in a specific environment.
/// </summary>
public sealed record DeploymentState(
    string ComponentName,
    string Environment,
    string Version,
    string ApkName,
    string AppId,
    DateTime DeployedAt,
    string DeployedBy)
{
    /// <summary>
    /// Creates a unique key for storing/retrieving deployment state.
    /// </summary>
    public string GetStateKey() => $"{ComponentName}_{Environment}";
}
