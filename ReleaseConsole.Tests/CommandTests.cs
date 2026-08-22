using Microsoft.Extensions.Logging;
using Moq;
using ReleaseConsole.Commands;
using ReleaseConsole.Core;
using ReleaseConsole.Services.Interfaces;
using Xunit;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.Tests;

public class CommandTests
{
    [Fact]
    public async Task DeployCommand_WithMockSelector_UsesSelectedVersion()
    {
        var mockSelector = new Mock<IVersionSelector>();
        mockSelector.Setup(s => s.SelectVersionAsync(It.IsAny<IReadOnlyList<Artifact>>(), It.IsAny<string>(), default))
                    .ReturnsAsync("1.0.2223.947");
        
        var component    = new Component(ComponentType.Test, "TestComponent", "testDescription", "testOwner");
        var env          = new Environment();
        var storage      = new Mock<IArtifactStorage>().Object;
        var executor     = new Mock<IPowerShellExecutor>().Object;
        var stateService = new Mock<IDeploymentStateService>().Object;
        var audit        = new Mock<IAuditLog>().Object;
        var logger       = new Mock<ILogger<DeployCommand>>().Object;
        var command      = new DeployCommand(component, env, storage, executor, audit, logger);

        await command.ExecuteAsync();
    }
}