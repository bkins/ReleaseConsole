using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ReleaseConsole.Core;
using ReleaseConsole.Services;
using Xunit;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.Tests.Services;

public sealed class JsonDeploymentStateServiceTests : IDisposable
{
    private readonly string _testStateFile;
    private readonly JsonDeploymentStateService _sut;

    public JsonDeploymentStateServiceTests()
    {
        _testStateFile = Path.Combine(Path.GetTempPath(), $"test-state-{Guid.NewGuid()}.json");
        _sut = new JsonDeploymentStateService(
            NullLogger<JsonDeploymentStateService>.Instance,
            _testStateFile);
    }

    public void Dispose()
    {
        if (File.Exists(_testStateFile))
            File.Delete(_testStateFile);
    }

    [Fact]
    public async Task GetCurrentStateAsync_WhenNoStateExists_ReturnsNull()
    {
        // Arrange
        var component = new Component(ComponentType.Laa, "TestApp", "TestApp");

        // Act
        var result = await _sut.GetCurrentStateAsync(component, Environment.Dev);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveStateAsync_ThenGetCurrentStateAsync_ReturnsCorrectState()
    {
        // Arrange
        var component = new Component(ComponentType.Laa, "TestApp", "TestApp");
        var expectedState = new DeploymentState(
            ComponentName: "Laa",
            Environment: "Dev",
            Version: "1.0.0",
            ApkName: "Laa-1.0.0-dev.apk",
            AppId: "com.snikpoh.localaiassistant.dev",
            DeployedAt: DateTime.UtcNow,
            DeployedBy: "TestUser");

        // Act
        await _sut.SaveStateAsync(expectedState);
        var result = await _sut.GetCurrentStateAsync(component, Environment.Dev);

        // Assert
        result.Should().NotBeNull();
        result!.ComponentName.Should().Be(expectedState.ComponentName);
        result.Version.Should().Be(expectedState.Version);
        result.ApkName.Should().Be(expectedState.ApkName);
        result.AppId.Should().Be(expectedState.AppId);
        result.DeployedBy.Should().Be(expectedState.DeployedBy);
    }

    [Fact]
    public async Task SaveStateAsync_UpdatesExistingState_WhenCalledTwiceForSameComponentAndEnvironment()
    {
        // Arrange
        var component = new Component(ComponentType.Laa, "TestApp", "TestApp");
        var firstState = new DeploymentState(
            ComponentName: "Laa",
            Environment: "Qa",
            Version: "1.0.0",
            ApkName: "Laa-1.0.0-qa.apk",
            AppId: "com.snikpoh.localaiassistant.qa",
            DeployedAt: DateTime.UtcNow.AddDays(-1),
            DeployedBy: "User1");

        var secondState = firstState with 
        { 
            Version = "2.0.0", 
            ApkName = "Laa-2.0.0-qa.apk",
            DeployedAt = DateTime.UtcNow 
        };

        // Act
        await _sut.SaveStateAsync(firstState);
        await _sut.SaveStateAsync(secondState);
        var result = await _sut.GetCurrentStateAsync(component, Environment.Qa);

        // Assert
        result.Should().NotBeNull();
        result!.Version.Should().Be("2.0.0");
        result.ApkName.Should().Be("Laa-2.0.0-qa.apk");
    }

    [Fact]
    public async Task GetAllStatesAsync_ReturnsAllSavedStates()
    {
        // Arrange
        var devState = new DeploymentState(
            "Laa", "Dev", "1.0.0", "Laa-1.0.0-dev.apk",
            "com.snikpoh.localaiassistant.dev", DateTime.UtcNow, "User1");

        var qaState = new DeploymentState(
            "Laa", "Qa", "1.0.0", "Laa-1.0.0-qa.apk",
            "com.snikpoh.localaiassistant.qa", DateTime.UtcNow, "User1");

        // Act
        await _sut.SaveStateAsync(devState);
        await _sut.SaveStateAsync(qaState);
        var results = await _sut.GetAllStatesAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(s => s.Environment == "Dev");
        results.Should().Contain(s => s.Environment == "Qa");
    }

    [Fact]
    public async Task SaveStateAsync_CreatesStateFile_WhenItDoesNotExist()
    {
        // Arrange
        var state = new DeploymentState(
            "Laa", "Dev", "1.0.0", "Laa-1.0.0-dev.apk",
            "com.snikpoh.localaiassistant.dev", DateTime.UtcNow, "TestUser");

        // Act
        await _sut.SaveStateAsync(state);

        // Assert
        File.Exists(_testStateFile).Should().BeTrue();
    }

    [Fact]
    public async Task StateService_HandlesConcurrentAccess()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 10)
            .Select(i => new DeploymentState(
                $"Component{i}", "Dev", $"1.{i}.0", $"app-{i}.apk",
                $"com.test.app{i}", DateTime.UtcNow, $"User{i}"))
            .Select(state => _sut.SaveStateAsync(state))
            .ToArray();

        // Act
        await Task.WhenAll(tasks);
        var results = await _sut.GetAllStatesAsync();

        // Assert
        results.Should().HaveCount(10);
    }
}
