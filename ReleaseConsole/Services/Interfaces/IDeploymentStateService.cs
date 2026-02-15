using ReleaseConsole.Core;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.Services.Interfaces;

/// <summary>
/// Manages deployment state tracking for components across environments.
/// </summary>
public interface IDeploymentStateService
{
    /// <summary>
    /// Retrieves the current deployment state for a component in a specific environment.
    /// </summary>
    /// <returns>The current deployment state, or null if no deployment exists.</returns>
    Task<DeploymentState?> GetCurrentStateAsync(
        Component component, 
        Environment environment, 
        CancellationToken ct = default);

    /// <summary>
    /// Saves the deployment state after a successful deployment.
    /// </summary>
    Task SaveStateAsync(
        DeploymentState state, 
        CancellationToken ct = default);

    /// <summary>
    /// Gets all deployment states for audit/reporting purposes.
    /// </summary>
    Task<IReadOnlyCollection<DeploymentState>> GetAllStatesAsync(
        CancellationToken ct = default);
}
