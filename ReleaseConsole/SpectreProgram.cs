using CP.Client.Core.Avails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReleaseConsole.Commands;
using ReleaseConsole.ConsoleUi;
using ReleaseConsole.Core;
using ReleaseConsole.Core.Spinners;
using ReleaseConsole.Menu;
using ReleaseConsole.MenuActions;
using ReleaseConsole.Services;
using ReleaseConsole.Services.Interfaces;
using Spectre.Console;
using System.CommandLine;
using System.Reflection;
using static ReleaseConsole.Menu.MenuText;
using static ReleaseConsole.Menu.MenuText.Commands;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole;

public class Program
{
    private static ServiceProvider? _serviceProvider;

    public static async Task<int> Main( string[] args )
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var services = ConfigureServices();
        _serviceProvider = services.BuildServiceProvider();

        if (args.Length == 0)
            return await RunSpectreMenuAsync();

        var rootCommand = BuildRootCommand(_serviceProvider);
        return await rootCommand.InvokeAsync(args);
    }

    public static async Task<T> RunWithSpinnerAsync<T>( string        message
                                                      , Func<Task<T>> action
                                                      , Color?        color = null )
    {
        return await AnsiConsole.Status()
                                .Spinner(Spinner.Known.Dots)
                                .SpinnerStyle(color ?? Color.White)
                                .StartAsync(message, async _ => await action());
    }

    public static async Task<T> RunWithSpinnerAsync<T>( string                        message
                                                      , Func<Action<string>, Task<T>> action
                                                      , Color?                        color         = null
                                                      , string                        messageSuffix = "" )
    {
        var suffix = messageSuffix.HasValue() ? $" - {messageSuffix}" : message;

        return await AnsiConsole.Status()
                                .Spinner(new CaseSwappingSpinner(message))
                                .SpinnerStyle(color ?? Color.White)
                                .StartAsync(suffix
                                          , async ctx =>
                                            {
                                                void Report(string text) => ctx.Status(text);
                                                return await action(Report);
                                            });
    }

    private static async Task<int> RunSpectreMenuAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();

            AnsiConsole.Write(new FigletText(Header.Title).LeftJustified().Color(Color.Cyan1));
            AnsiConsole.MarkupLine(Header.Subtitle + "\n");

            var menu = new ConsoleMenu
                       {
                               Title = MainMenuTitle
                             , Hint  = Navigation.MoreChoicesHint
                       }
                      .Add(BuildComponent.Label
                         , () => _serviceProvider!.GetRequiredService<BuildComponentMenuAction>().ExecuteAsync())
                      .Add(DeployComponent.Label
                         , () => _serviceProvider!.GetRequiredService<DeployComponentMenuAction>().ExecuteAsync())
                      .Add(VerifyEnvironment.Label
                         , () => _serviceProvider!.GetRequiredService<VerifyEnvironmentMenuAction>().ExecuteAsync())
                      .Add(ViewAuditLogs.Label
                         , () => _serviceProvider!.GetRequiredService<ViewAuditLogsMenuAction>().ExecuteAsync())
                      .Add(ListArtifacts.Label
                         , () => _serviceProvider!.GetRequiredService<ListArtifactsMenuAction>().ExecuteAsync())
                      .Add(LaunchScalar.Label
                         , () => _serviceProvider!.GetRequiredService<LaunchScalarMenuAction>().ExecuteAsync())
                      .Add(DbAction.Label
                         , () => _serviceProvider!.GetRequiredService<DbActionsMenuAction>().ExecuteAsync())
                      .AddExit(Navigation.Exit);

            var result = await menu.ShowAsync();

            if (result != MenuResult.Exit) continue;

            AnsiConsole.MarkupLine(Navigation.Goodby);
            return 0;
        }
    }

    private static void DisplayCommandLineResult( CommandResult result )
    {
        Console.WriteLine();
        Console.WriteLine(result.Success ? Results.Success : Results.Failure);
        Console.WriteLine(result.Message);

        if (result.ErrorDetails?.HasValue() ?? false)
        {
            Console.WriteLine();
            Console.WriteLine("Details:");
            Console.WriteLine(result.ErrorDetails);
        }

        Console.WriteLine();
    }

    private static RootCommand BuildRootCommand( ServiceProvider serviceProvider )
    {
        var rootCommand = new RootCommand("ReleaseConsole - Deliberate deployment control system");

        rootCommand.Add(BuildBuildCommand(serviceProvider));
        rootCommand.Add(BuildDeployCommand(serviceProvider));
        rootCommand.Add(BuildVerifyCommand(serviceProvider));

        return rootCommand;
    }

    private static Command BuildBuildCommand( ServiceProvider serviceProvider )
    {
        var componentArg = new Argument<string>("component") { Description = "Component to build" };

        var envOption = new Option<string>("--env", "Target environment (dev, qa, prod)")
                        { IsRequired = true };

        var command = new Command("build", "Build a component and create a versioned artifact")
                      { componentArg, envOption };

        command.SetHandler(async ( componentName, envName ) =>
                           {
                               try
                               {
                                   var component   = Component.FromString(componentName);
                                   var environment = ParseEnvironment(envName);

                                   var buildCommand = new BuildCommand(component
                                                                     , environment
                                                                     , serviceProvider.GetRequiredService<IArtifactStorage>()
                                                                     , serviceProvider.GetRequiredService<IPowerShellExecutor>()
                                                                     , serviceProvider.GetRequiredService<IAuditLog>()
                                                                     , serviceProvider.GetRequiredService<ILogger<BuildCommand>>());

                                   var result = await buildCommand.ExecuteAsync();
                                   DisplayCommandLineResult(result);
                               }
                               catch (Exception ex)
                               {
                                   Console.WriteLine($"ERROR: {ex.Message}");
                               }
                           }
                         , componentArg
                         , envOption);

        return command;
    }

    private static Command BuildDeployCommand( ServiceProvider serviceProvider )
    {
        var componentArg = new Argument<string>("component", "Component to deploy");

        var envOption = new Option<string>("--env", "Target environment (dev, qa, prod)")
                        { IsRequired = true };

        var versionOption = new Option<string?>("--version", "Specific version to deploy (optional - defaults to latest)")
                            { IsRequired = false };

        var command = new Command("deploy", "Deploy an artifact to an environment")
                      { componentArg, envOption, versionOption };

        command.SetHandler(async ( componentName, envName, version ) =>
                           {
                               try
                               {
                                   var component   = Component.FromString(componentName);
                                   var environment = ParseEnvironment(envName);

                                   var deployCommand = new DeployCommand(component
                                                                       , environment
                                                                       , serviceProvider.GetRequiredService<IArtifactStorage>()
                                                                       , serviceProvider.GetRequiredService<IPowerShellExecutor>()
                                                                       , serviceProvider.GetRequiredService<IAuditLog>()
                                                                       , serviceProvider.GetRequiredService<ILogger<DeployCommand>>()
                                                                       , scriptsPath:   null
                                                                       , targetVersion: version);

                                   var result = await deployCommand.ExecuteAsync();
                                   DisplayCommandLineResult(result);
                               }
                               catch (Exception ex)
                               {
                                   Console.WriteLine($"ERROR: {ex.Message}");
                               }
                           }
                         , componentArg
                         , envOption
                         , versionOption);

        return command;
    }

    private static Command BuildVerifyCommand( ServiceProvider serviceProvider )
    {
        var envOption = new Option<string>("--env", "Environment to verify (dev, qa, prod)")
                        { IsRequired = true };

        var command = new Command("verify", "Verify environment health") { envOption };

        command.SetHandler(async envName =>
                           {
                               try
                               {
                                   var environment = ParseEnvironment(envName);

                                   var verifyCommand = new VerifyCommand(environment
                                                                       , serviceProvider.GetRequiredService<IAuditLog>()
                                                                       , serviceProvider.GetRequiredService<ILogger<VerifyCommand>>()
                                                                       , serviceProvider.GetRequiredService<HttpClient>());

                                   var result = await verifyCommand.ExecuteAsync();
                                   DisplayCommandLineResult(result);
                               }
                               catch (Exception ex)
                               {
                                   Console.WriteLine($"ERROR: {ex.Message}");
                               }
                           }
                         , envOption);

        return command;
    }

    private static Environment ParseEnvironment( string envName ) => envName.ToLowerInvariant() switch
    {
            "dev"  => Environment.Dev
          , "qa"   => Environment.Qa
          , "prod" => Environment.Prod
          , _      => throw new ArgumentException($"Invalid environment: {envName}")
    };

    private static ServiceCollection ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSingleton<IArtifactStorage, LocalArtifactStorage>();
        services.AddSingleton<IAuditLog, FileAuditLog>();
        services.AddTransient<IPowerShellExecutor>(_ => new PowerShellExecutor(ScriptOutputMode.ErrorsOnly));
        services.AddSingleton<IVersionSelector, ConsoleVersionSelector>();
        services.AddSingleton<IDeploymentStateService, JsonDeploymentStateService>();
        services.AddSingleton<HttpClient>();

        services.AddSingleton<IConsolePromptService, ConsolePromptService>();
        services.AddSingleton<IConsoleResultRenderer, ConsoleResultRenderer>();
        services.AddSingleton<IConsolePauseService, ConsolePauseService>();
        services.AddSingleton<ICommandExecutionPipeline, ConsoleCommandRunner>();

        services.AddTransient<BuildComponentMenuAction>();
        services.AddTransient<DeployComponentMenuAction>();
        services.AddTransient<VerifyEnvironmentMenuAction>();
        services.AddTransient<ViewAuditLogsMenuAction>();
        services.AddTransient<ListArtifactsMenuAction>();
        services.AddTransient<LaunchScalarMenuAction>();
        services.AddTransient<DbActionsMenuAction>();

        return services;
    }

    public static async Task ShowAllSpinnersAsync()
    {
        var knownSpinners = typeof(Spinner.Known).GetProperties(BindingFlags.Public | BindingFlags.Static)
                                                 .Where(propertyInfo => propertyInfo.PropertyType == typeof(Spinner))
                                                 .Select(propertyInfo => new
                                                                         {
                                                                                 Name    = propertyInfo.Name
                                                                               , Spinner = (Spinner)propertyInfo.GetValue(null)!
                                                                         });

        var custom = new
                     {
                             Name    = "CustomCaseSwapper"
                           , Spinner = (Spinner)new CaseSwappingSpinner("Thinking")
                     };

        var allSpinners = new[] { custom }.Concat(knownSpinners);

        foreach (var item in allSpinners)
        {
            await AnsiConsole.Status()
                             .Spinner(item.Spinner)
                             .SpinnerStyle(Style.Parse("cyan"))
                             .StartAsync($"Spinner: {item.Name.EscapeMarkup()}"
                                       , async _ => await Task.Delay(20000));
        }

        AnsiConsole.MarkupLine("\n[grey]Done previewing spinners. Press any key…[/]");
        Console.ReadKey();
    }
}
