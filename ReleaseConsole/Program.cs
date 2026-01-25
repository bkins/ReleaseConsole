// Program.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using ReleaseConsole.Commands;
using ReleaseConsole.Core;
using ReleaseConsole.Services;
using System.CommandLine;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var services = ConfigureServices();
        var serviceProvider = services.BuildServiceProvider();

        var rootCommand = BuildRootCommand(serviceProvider);
        return await rootCommand.InvokeAsync(args);
    }

    private static ServiceCollection ConfigureServices()
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Core Services
        services.AddSingleton<IArtifactStorage, LocalArtifactStorage>();
        services.AddSingleton<IAuditLog, FileAuditLog>();
        services.AddSingleton<IPowerShellExecutor, PowerShellExecutor>();
        services.AddSingleton<HttpClient>();

        return services;
    }

    private static RootCommand BuildRootCommand(ServiceProvider serviceProvider)
    {
        var rootCommand = new RootCommand("ReleaseConsole - Deliberate deployment control system");

        rootCommand.Add(BuildBuildCommand(serviceProvider));
        rootCommand.Add(BuildDeployCommand(serviceProvider));
        rootCommand.Add(BuildPromoteCommand(serviceProvider));
        rootCommand.Add(BuildVerifyCommand(serviceProvider));
        rootCommand.Add(BuildRollbackCommand(serviceProvider));

        return rootCommand;
    }

    private static Command BuildBuildCommand(ServiceProvider serviceProvider)
    {
        var componentArg = new Argument<string>("component");
        componentArg.Description = $"Component to build (acceptable component types: '{nameof(Component.CpApi)}', '{nameof(Component.LaaMauiApp)}' or \"maui\", `{nameof(Component.CpSharedPrimitives)} or \"lib1\"`, or '{nameof(Component.CpClientCore)} or \"lib2\"' )\"";
        
        var envOption = new Option<string>("--env"
                                          , "Target environment (dev, qa)")
                        {
                                IsRequired = true
                        };

        var command = new Command("build", "Build a component and create a versioned artifact")
        {
            componentArg,
            envOption
        };

        command.SetHandler(async (componentName, envName) =>
        {
            try
            {
                var component = Component.FromString(componentName);
                var environment = ParseEnvironment(envName);

                if (environment == Core.Environment.Prod)
                {
                    Console.WriteLine("ERROR: Cannot build directly for Prod. Use 'promote' instead.");
                    return;
                }

                var buildCommand = new BuildCommand(component
                                                  , environment
                                                  , serviceProvider.GetRequiredService<IArtifactStorage>()
                                                  , serviceProvider.GetRequiredService<IPowerShellExecutor>()
                                                  , serviceProvider.GetRequiredService<IAuditLog>()
                                                  , serviceProvider.GetRequiredService<ILogger<BuildCommand>>());

                var result = await buildCommand.ExecuteAsync();
                DisplayResult(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
        }, componentArg, envOption);

        return command;
    }

    private static Command BuildDeployCommand(ServiceProvider serviceProvider)
    {
        var componentArg = new Argument<string>(
            "component",
            "Component to deploy"
        );

        var envOption = new Option<string>(
            "--env",
            "Target environment (dev, qa, prod)"
        ) { IsRequired = true };

        var command = new Command("deploy"
                                , "Deploy the latest artifact to an environment")
                      {
                              componentArg
                            , envOption
                      };

        command.SetHandler(async (componentName, envName) =>
        {
            try
            {
                var component = Component.FromString(componentName);
                var environment = ParseEnvironment(envName);

                var deployCommand = new DeployCommand(component
                                                    , environment
                                                    , serviceProvider.GetRequiredService<IArtifactStorage>()
                                                    , serviceProvider.GetRequiredService<IPowerShellExecutor>()
                                                    , serviceProvider.GetRequiredService<IAuditLog>()
                                                    , serviceProvider.GetRequiredService<ILogger<DeployCommand>>());

                var result = await deployCommand.ExecuteAsync();
                DisplayResult(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
        }, componentArg, envOption);

        return command;
    }

    private static Command BuildPromoteCommand(ServiceProvider serviceProvider)
    {
        var componentArg = new Argument<string>(
            "component",
            "Component to promote"
        );

        var justificationOption = new Option<string>(
            "--reason",
            "Justification for production promotion"
        ) { IsRequired = true };

        var command = new Command("promote", "Promote a QA artifact to Production")
        {
            componentArg,
            justificationOption
        };

        command.SetHandler(async (componentName, justification) =>
        {
            try
            {
                var component = Component.FromString(componentName);

                var promoteCommand = new PromoteCommand(
                    component,
                    justification,
                    serviceProvider.GetRequiredService<IArtifactStorage>(),
                    serviceProvider.GetRequiredService<IAuditLog>(),
                    serviceProvider.GetRequiredService<ILogger<PromoteCommand>>()
                );

                var result = await promoteCommand.ExecuteAsync();
                DisplayResult(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
        }, componentArg, justificationOption);

        return command;
    }

    private static Command BuildVerifyCommand(ServiceProvider serviceProvider)
    {
        var envOption = new Option<string>(
            "--env",
            "Environment to verify (dev, qa, prod)"
        ) { IsRequired = true };

        var command = new Command("verify", "Verify environment health")
        {
            envOption
        };

        command.SetHandler(async (envName) =>
        {
            try
            {
                var environment = ParseEnvironment(envName);

                var verifyCommand = new VerifyCommand(
                    environment,
                    serviceProvider.GetRequiredService<IAuditLog>(),
                    serviceProvider.GetRequiredService<ILogger<VerifyCommand>>(),
                    serviceProvider.GetRequiredService<HttpClient>()
                );

                var result = await verifyCommand.ExecuteAsync();
                DisplayResult(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
        }, envOption);

        return command;
    }

    private static Command BuildRollbackCommand(ServiceProvider serviceProvider)
    {
        var componentArg = new Argument<string>(
            "component",
            "Component to rollback"
        );

        var versionOption = new Option<string>(
            "--to",
            "Target version to rollback to"
        ) { IsRequired = true };

        var command = new Command("rollback", "Rollback to a previous production version")
        {
            componentArg,
            versionOption
        };

        command.SetHandler(async (componentName, version) =>
        {
            try
            {
                var component = Component.FromString(componentName);

                var rollbackCommand = new RollbackCommand(
                    component,
                    version,
                    serviceProvider.GetRequiredService<IArtifactStorage>(),
                    serviceProvider.GetRequiredService<IPowerShellExecutor>(),
                    serviceProvider.GetRequiredService<IAuditLog>(),
                    serviceProvider.GetRequiredService<ILogger<RollbackCommand>>()
                );

                var result = await rollbackCommand.ExecuteAsync();
                DisplayResult(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
        }, componentArg, versionOption);

        return command;
    }

    private static Core.Environment ParseEnvironment(string envName) => envName.ToLowerInvariant() switch
    {
        "dev" => Core.Environment.Dev,
        "qa" => Core.Environment.Qa,
        "prod" => Core.Environment.Prod,
        _ => throw new ArgumentException($"Invalid environment: {envName}")
    };

    private static void DisplayResult(CommandResult result)
    {
        Console.WriteLine();
        Console.WriteLine(result.Success ? "✓ SUCCESS" : "✗ FAILURE");
        Console.WriteLine(result.Message);

        if (!string.IsNullOrEmpty(result.ErrorDetails))
        {
            Console.WriteLine();
            Console.WriteLine("Details:");
            Console.WriteLine(result.ErrorDetails);
        }

        Console.WriteLine();
    }
}
