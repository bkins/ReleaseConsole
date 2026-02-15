using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReleaseConsole.Commands;
using ReleaseConsole.Core;
using ReleaseConsole.Services;
using ReleaseConsole.Services.Interfaces;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole;

public class InteractiveMenu
{
    private readonly ServiceProvider _serviceProvider;
    
    private bool _running = true;

    public InteractiveMenu(ServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task RunAsync()
    {
        Console.Clear();
        ShowBanner();

        while (_running)
        {
            ShowMainMenu();
            var choice = ReadChoice();

            await ProcessChoiceAsync(choice);
        }
    }

    private static void ShowBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    RELEASE CONSOLE                         ║");
        Console.WriteLine("║          Deliberate Deployment Control System              ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void ShowMainMenu()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("═══════════════ MAIN MENU ═══════════════");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("  [1] Build Component");
        Console.WriteLine("  [2] Deploy Component");
        Console.WriteLine("  [3] Promote to Production");
        Console.WriteLine("  [4] Verify Environment");
        Console.WriteLine("  [5] Rollback Production");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  [A] View Audit Log");
        Console.WriteLine("  [L] List Artifacts");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("  [Q] Quit");
        Console.WriteLine();
        Console.Write("Select option: ");
    }

    private static string ReadChoice()
    {
        var input = Console.ReadKey(true);
        Console.WriteLine(input.KeyChar);
        
        return input.KeyChar.ToString().ToUpperInvariant();
    }

    private async Task ProcessChoiceAsync(string choice)
    {
        Console.WriteLine();

        switch (choice)
        {
            case "1":
                await BuildComponentAsync();
                break;
            case "2":
                await DeployComponentAsync();
                break;
            case "3":
                await PromoteToProductionAsync();
                break;
            case "4":
                await VerifyEnvironmentAsync();
                break;
            case "5":
                await RollbackProductionAsync();
                break;
            case "A":
                await ViewAuditLogAsync();
                break;
            case "L":
                await ListArtifactsAsync();
                break;
            case "Q":
                _running = false;
                Console.WriteLine("Goodbye!");
                break;
            default:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid option. Please try again.");
                Console.ResetColor();
                break;
        }

        if (_running && choice != "Q")
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Press any key to continue...");
            Console.ResetColor();
            Console.ReadKey(true);
            Console.Clear();
            
            ShowBanner();
        }
    }

    private async Task FixAufitLogAsync()
    {
        
    }

    private async Task BuildComponentAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("═══ BUILD COMPONENT ═══");
        Console.ResetColor();
        Console.WriteLine();

        var component = SelectComponent();
        if (component == null) return;

        var environment = SelectEnvironment(excludeProd: true);
        if (environment == null) return;

        if (environment == Environment.Prod)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: Cannot build directly for Prod. Use 'Promote' instead.");
            Console.ResetColor();
            return;
        }

        try
        {
            var command = new BuildCommand(component
                                         , environment.Value
                                         , _serviceProvider.GetRequiredService<IArtifactStorage>()
                                         , _serviceProvider.GetRequiredService<IPowerShellExecutor>()
                                         , _serviceProvider.GetRequiredService<IAuditLog>()
                                         , _serviceProvider.GetRequiredService<ILogger<BuildCommand>>());

            var result = await command.ExecuteAsync();
            DisplayResult(result);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.ResetColor();
        }
    }

    private async Task DeployComponentAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("═══ DEPLOY COMPONENT ═══");
        Console.ResetColor();
        Console.WriteLine();

        var component = SelectComponent();
        if (component == null) return;

        var environment = SelectEnvironment();
        if (environment == null) return;

        try
        {
            var command = new DeployCommand(component
                                          , environment.Value
                                          , _serviceProvider.GetRequiredService<IArtifactStorage>()
                                          , _serviceProvider.GetRequiredService<IPowerShellExecutor>()
                                          , _serviceProvider.GetRequiredService<IDeploymentStateService>()
                                          , _serviceProvider.GetRequiredService<IAuditLog>()
                                          , _serviceProvider.GetRequiredService<ILogger<DeployCommand>>());

            var result = await command.ExecuteAsync();
            DisplayResult(result);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.ResetColor();
        }
    }

    private async Task PromoteToProductionAsync()
    {
        // TODO: Need to be able to choose which artifact to promote if there are multiple dev/qa builds.
        //  Maybe show last 5 builds with timestamps and let user select?
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("═══ PROMOTE TO PRODUCTION ═══");
        Console.ResetColor();
        Console.WriteLine();

        var component = SelectComponent();
        if (component == null) return;

        Console.Write("Justification (required): ");
        var justification = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(justification))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: Justification is required for production promotion.");
            Console.ResetColor();
            return;
        }

        try
        {
            var command = new PromoteCommand(component
                                           , justification
                                           , _serviceProvider.GetRequiredService<IArtifactStorage>()
                                           , _serviceProvider.GetRequiredService<IAuditLog>()
                                           , _serviceProvider.GetRequiredService<ILogger<PromoteCommand>>());
            
            var result = await command.ExecuteAsync();
            DisplayResult(result);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.ResetColor();
        }
    }

    private async Task VerifyEnvironmentAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("═══ VERIFY ENVIRONMENT ═══");
        Console.ResetColor();
        Console.WriteLine();

        var environment = SelectEnvironment();
        if (environment == null) return;

        try
        {
            var command = new VerifyCommand(
                environment.Value,
                _serviceProvider.GetRequiredService<IAuditLog>(),
                _serviceProvider.GetRequiredService<ILogger<VerifyCommand>>(),
                _serviceProvider.GetRequiredService<HttpClient>()
            );

            var result = await command.ExecuteAsync();
            DisplayResult(result);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.ResetColor();
        }
    }

    private async Task RollbackProductionAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("═══ ROLLBACK PRODUCTION ═══");
        Console.ResetColor();
        Console.WriteLine();

        var component = SelectComponent();
        if (component == null) return;

        // Show available production artifacts
        var storage   = _serviceProvider.GetRequiredService<IArtifactStorage>();
        var artifacts = await storage.GetAllArtifactsAsync(component, Environment.Prod);
        var prodArtifacts = artifacts.Where(a => a.Metadata.BuiltFor == Environment.Prod)
                                     .OrderByDescending(a => a.Metadata.BuildTimestamp)
                                     .ToList();

        if ( ! prodArtifacts.Any())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"No production artifacts found for {component.Name}");
            Console.ResetColor();
            
            return;
        }

        Console.WriteLine("Available production versions:");
        for (int i = 0; i < prodArtifacts.Count && i < 10; i++)
        {
            var artifact = prodArtifacts[i];
            Console.WriteLine($"  [{i + 1}] {artifact.Version} - {artifact.Metadata.BuildTimestamp:yyyy-MM-dd HH:mm}");
        }
        Console.WriteLine();

        Console.Write("Select version number (or type version string): ");
        var versionInput = Console.ReadLine()?.Trim();

        string? targetVersion = null;

        if (int.TryParse(versionInput, out int versionIndex) 
         && versionIndex > 0 
         && versionIndex <= prodArtifacts.Count)
        {
            targetVersion = prodArtifacts[versionIndex - 1].Version;
        }
        else
        {
            targetVersion = versionInput;
        }

        if (string.IsNullOrWhiteSpace(targetVersion))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: Invalid version selection.");
            Console.ResetColor();
            return;
        }

        try
        {
            var command = new RollbackCommand(component
                                            , targetVersion
                                            , _serviceProvider.GetRequiredService<IArtifactStorage>()
                                            , _serviceProvider.GetRequiredService<IPowerShellExecutor>()
                                            , _serviceProvider.GetRequiredService<IAuditLog>()
                                            , _serviceProvider.GetRequiredService<ILogger<RollbackCommand>>());

            var result = await command.ExecuteAsync();
            DisplayResult(result);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.ResetColor();
        }
    }

    private async Task ViewAuditLogAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("═══ AUDIT LOG (Last 20 Entries) ═══");
        Console.ResetColor();
        Console.WriteLine();

        var auditLog = _serviceProvider.GetRequiredService<IAuditLog>();
        var entries = await auditLog.GetRecentEntriesAsync(20);

        if ( ! entries.Any())
        {
            Console.WriteLine("No audit entries found.");
            return;
        }

        foreach (var entry in entries)
        {
            var color = entry.Success ? ConsoleColor.Green : ConsoleColor.Red;
            var status = entry.Success ? "✓" : "✗";

            Console.ForegroundColor = color;
            Console.Write(status);
            Console.ResetColor();
            Console.Write($" {entry.Timestamp:yyyy-MM-dd HH:mm:ss} ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(entry.Command.PadRight(10));
            Console.ResetColor();
            Console.Write($" {entry.Component.PadRight(15)}");
            
            if (entry.Environment.HasValue)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($" {entry.Environment.Value.ToString().PadRight(6)}");
                Console.ResetColor();
            }
            
            Console.WriteLine($" {entry.User}");
        }
    }

    private async Task ListArtifactsAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("═══ ARTIFACTS ═══");
        Console.ResetColor();
        Console.WriteLine();

        var component = SelectComponent();
        if (component == null) return;

        var storage       = _serviceProvider.GetRequiredService<IArtifactStorage>();
        var artifactsDev  = await storage.GetAllArtifactsAsync(component, Environment.Dev);
        var artifactsQa   = await storage.GetAllArtifactsAsync(component, Environment.Qa);
        var artifactsProd = await storage.GetAllArtifactsAsync(component, Environment.Prod);
        var artifacts     = artifactsDev.Concat(artifactsQa)
                                        .Concat(artifactsProd)
                                        .ToList();
        if (!artifacts.Any())
        {
            Console.WriteLine($"No artifacts found for {component.Name}");
            return;
        }

        Console.WriteLine($"Artifacts for {component.Name}:");
        Console.WriteLine();

        var grouped = artifacts.GroupBy(artifact => artifact.Metadata.BuiltFor)
                               .OrderBy(grouping => grouping.Key);

        foreach (var group in grouped)
        {
            Console.ForegroundColor = group.Key switch
            {
                    Environment.Dev => ConsoleColor.Green
                  , Environment.Qa => ConsoleColor.Yellow
                  , Environment.Prod => ConsoleColor.Red
                  , _ => ConsoleColor.White
            };
            Console.WriteLine($"  {group.Key}:");
            Console.ResetColor();

            foreach (var artifact in group.OrderByDescending(a => a.Metadata.BuildTimestamp))
            {
                Console.WriteLine($"    {artifact.Version} - {artifact.Metadata.BuildTimestamp:yyyy-MM-dd HH:mm:ss}");
            }
            Console.WriteLine();
        }
    }

    private Component? SelectComponent()
    {
        Console.WriteLine("Select Component:");
        var components = Component.All.ToList();

        for (int i = 0; i < components.Count; i++)
        {
            Console.WriteLine($"  [{i + 1}] {components[i].Name} - {components[i].Description}");
        }
        Console.WriteLine();
        Console.Write("Choice: ");

        var input = Console.ReadLine();
        if (int.TryParse(input, out int choice) 
         && choice > 0 
         && choice <= components.Count)
        {
            return components[choice - 1];
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Invalid selection.");
        Console.ResetColor();
        return null;
    }

    private Environment? SelectEnvironment(bool excludeProd = false)
    {
        Console.WriteLine("Select Environment:");
        Console.WriteLine("  [1] Dev");
        Console.WriteLine("  [2] QA");
        if ( ! excludeProd)
        {
            Console.WriteLine("  [3] Prod");
        }
        Console.WriteLine();
        Console.Write("Choice: ");

        var input = Console.ReadLine();
        if (int.TryParse(input, out int choice))
        {
            return choice switch
            {
                1 => Environment.Dev,
                2 => Environment.Qa,
                3 when !excludeProd => Environment.Prod,
                _ => null
            };
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Invalid selection.");
        Console.ResetColor();
        return null;
    }

    private static void DisplayResult(CommandResult result)
    {
        Console.WriteLine();
        if (result.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ SUCCESS");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("✗ FAILURE");
        }
        Console.ResetColor();

        Console.WriteLine(result.Message);

        if ( ! string.IsNullOrEmpty(result.ErrorDetails))
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Details:");
            Console.ResetColor();
            Console.WriteLine(result.ErrorDetails);
        }

        Console.WriteLine();
    }
}
