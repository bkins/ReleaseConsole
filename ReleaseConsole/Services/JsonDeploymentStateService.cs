using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReleaseConsole.Core;
using ReleaseConsole.Services.Interfaces;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.Services;

/// <summary>
/// File-based deployment state service using JSON for persistence.
/// Thread-safe implementation with file locking.
/// </summary>
public sealed class JsonDeploymentStateService : IDeploymentStateService
{
    private readonly string                              _stateFilePath;
    private readonly ILogger<JsonDeploymentStateService> _logger;
    private readonly SemaphoreSlim                       _fileLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonDeploymentStateService( ILogger<JsonDeploymentStateService> logger,
                                       string? stateFilePath = null)
    {
        _logger = logger;
        _stateFilePath = stateFilePath ?? GetDefaultStateFilePath();
        
        EnsureStateFileExists();
    }

    public async Task<DeploymentState?> GetCurrentStateAsync( Component         component, 
                                                              Environment       environment, 
                                                              CancellationToken ct = default)
    {
        await _fileLock.WaitAsync(ct);
        try
        {
            var states = await ReadStatesAsync(ct);
            var key = CreateStateKey(component.Name, environment);
            
            return states.TryGetValue(key, out var state) ? state : null;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveStateAsync( DeploymentState   state, 
                                      CancellationToken ct = default)
    {
        await _fileLock.WaitAsync(ct);
        try
        {
            var states = await ReadStatesAsync(ct);
            var key = state.GetStateKey();
            
            states[key] = state;
            
            await WriteStatesAsync(states, ct);
            
            // _logger.LogInformation("Saved deployment state: {Component} v{Version} to {Environment}"
            //                      , state.ComponentName
            //                      , state.Version
            //                      , state.Environment);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<IReadOnlyCollection<DeploymentState>> GetAllStatesAsync( CancellationToken ct = default)
    {
        await _fileLock.WaitAsync(ct);
        try
        {
            var states = await ReadStatesAsync(ct);
            return states.Values.ToList();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<Dictionary<string, DeploymentState>> ReadStatesAsync(CancellationToken ct)
    {
        if (!File.Exists(_stateFilePath))
            return new Dictionary<string, DeploymentState>();

        var json = await File.ReadAllTextAsync(_stateFilePath, ct);
        
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, DeploymentState>();

        return JsonSerializer.Deserialize<Dictionary<string, DeploymentState>>(json, JsonOptions)
               ?? new Dictionary<string, DeploymentState>();
    }

    private async Task WriteStatesAsync( Dictionary<string, DeploymentState> states, 
                                         CancellationToken                   ct)
    {
        var json = JsonSerializer.Serialize(states, JsonOptions);
        await File.WriteAllTextAsync(_stateFilePath, json, ct);
    }

    private void EnsureStateFileExists()
    {
        var directory = Path.GetDirectoryName(_stateFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogInformation("Created state directory: {Directory}", directory);
        }

        if (!File.Exists(_stateFilePath))
        {
            File.WriteAllText(_stateFilePath, "{}");
            _logger.LogInformation("Created deployment state file: {FilePath}", _stateFilePath);
        }
    }

    private static string CreateStateKey(string componentName, Environment environment) =>
        $"{componentName}_{environment}";

    private static string GetDefaultStateFilePath() =>
        Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "ReleaseConsole",
            "deployment-state.json");
}
