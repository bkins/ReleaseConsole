using Microsoft.Extensions.Logging;
using Moq;
using ReleaseConsole.Core;
using ReleaseConsole.Services;
using ReleaseConsole.Services.Interfaces;
using Environment = System.Environment;

namespace ReleaseConsole.Commands;

public sealed class VerifyCommand : CommandBaseCommand
{
    private readonly ReleaseConsole.Core.Environment _environment;
    private readonly HttpClient                      _httpClient;

    private const string HostRoot = "http://localhost";
    
    public VerifyCommand( ReleaseConsole.Core.Environment environment
                        , IAuditLog                       auditLog
                        , ILogger<VerifyCommand>          logger
                        , HttpClient?                     httpClient = null )
            : base(auditLog
                 , logger)
    {
        _environment = environment;
        _httpClient  = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public override string Name        => "verify";
    public override string Description => $"Verify {_environment} environment health";

    protected override ReleaseConsole.Core.Environment? GetEnvironment() => _environment;

    protected override async Task<CommandResult> ExecuteInternalAsync(CancellationToken ct)
    {
        // Logger.LogInformation("Verifying {Environment} environment", _environment);

        var checks = new List<(string Name, bool Success, string Details)>();

        // HTTP Health Check
        var (httpSuccess, httpDetails) = await PerformHttpHealthCheckAsync(ct);
        checks.Add(("HTTP Health Check", httpSuccess, httpDetails));

        // File Existence Check  -- Disabled for now
        // var (fileSuccess, fileDetails) = PerformFileExistenceCheck();
        // checks.Add(("File Existence Check", fileSuccess, fileDetails));

        // Database Connectivity (placeholder)
        var (dbSuccess, dbDetails) = await PerformDatabaseCheckAsync(ct);
        checks.Add(("Database Check", dbSuccess, dbDetails));

        // Build result
        var allSuccess    = checks.All(check => check.Success);
        var resultMessage = BuildResultMessage(checks);

        return allSuccess
                       ? CommandResult.Ok(resultMessage)
                       : CommandResult.Fail(resultMessage);
    }

    private async Task<(bool Success, string Details)> PerformHttpHealthCheckAsync(CancellationToken ct)
    {
        var endpoint = GetHealthEndpoint(_environment);
        if (endpoint is null)
        {
            return (true, "No health endpoint configured");
        }

        try
        {
            // Logger.LogInformation("Checking health endpoint: {Endpoint}", endpoint);
            var response = await _httpClient.GetAsync(endpoint, ct);
            
            return response.IsSuccessStatusCode
                           ? (true, $"HTTP {(int)response.StatusCode} - OK")
                           : (false, $"HTTP {(int)response.StatusCode} - {response.ReasonPhrase}");

        }
        catch (Exception ex)
        {
            return (false, $"Failed: {ex.Message}");
        }
    }

    private (bool Success, string Details) PerformFileExistenceCheck()
    {
        var expectedPath = GetExpectedDeploymentPath();
        if (expectedPath is null)
        {
            return (true, "No deployment path configured");
        }

        var exists = Directory.Exists(expectedPath) || File.Exists(expectedPath);
        return exists 
                       ? (true, $"Path exists: {expectedPath}")
                       : (false, $"Path not found: {expectedPath}");
    }

    private async Task<(bool Success, string Details)> PerformDatabaseCheckAsync(CancellationToken ct)
    {
        // Placeholder - implement based on your database
        await Task.CompletedTask;
        return (true, "Database check not implemented (placeholder)");
    }

    public static string? GetHealthEndpoint(ReleaseConsole.Core.Environment environment)
    {
        return ApiEnvironments.Url(environment) + "/health/ready";
    }
    //   Core.Environment.Dev  => ApiEnvironments.Url(_environment) //$"{HostRoot}:5273/health/ready"
    // , Core.Environment.Qa   => $"{HostRoot}:5274/health/ready"
    // , Core.Environment.Prod => $"{HostRoot}:5275/health/ready"
    // , _                     => null


    private string? GetExpectedDeploymentPath()
    {
        throw new NotImplementedException("GetExpectedDeploymentPath is not implemented yet. Need to define the correct deployment paths.");
        return _environment switch
        {
                Core.Environment.Dev => Path.Combine(Environment.CurrentDirectory
                                                                  , "publish")
              , Core.Environment.Qa => @"\\qa-server\deployments"
              , Core.Environment.Prod => @"\\prod-server\deployments", _ => null
        };
    }

    private string BuildResultMessage(List<(string Name, bool Success, string Details)> checks)
    {
        var lines = new List<string> { $"Verification Results: {_environment}" };
        foreach (var (name, success, details) in checks)
        {
            var status = success ? "✅" : "⛔";
            lines.Add($"  {status}  {name}: {details}");
        }
        return string.Join(System.Environment.NewLine, lines);
    }
}