// Program.cs - Fixed Spectre.Console Version
// Fixes: 1) Uses ASCII symbols instead of emojis, 2) No spinner during PowerShell execution

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReleaseConsole.Commands;
using ReleaseConsole.Core;
using ReleaseConsole.Services;
using Spectre.Console;
using System.CommandLine;
using System.Diagnostics;
using System.Reflection;
using CP.Client.Core.Avails;
using CP.Client.Core.Common.ConectivityToApi;
using ReleaseConsole.Core.Spinners;
using ReleaseConsole.Menu;
using ReleaseConsole.Services.Interfaces;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole;

public class Program
{
    private static ServiceProvider? _serviceProvider;

    private const string ScriptFolder = @"C:\CP\Deploy\Api\";
    private const string ScriptFileName = "start-api.bat";

    public static async Task<int> Main( string[] args )
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var services = ConfigureServices();
        _serviceProvider = services.BuildServiceProvider();

        // If no arguments, run Spectre.Console interactive menu
        if (args.Length == 0)
        {
            return await RunSpectreMenuAsync();
        }

        // Otherwise use System.CommandLine
        var rootCommand = BuildRootCommand(_serviceProvider);
        
        return await rootCommand.InvokeAsync(args);
    }

    private sealed record PromptOption<T>( string Label
                                         , T      Value
                                         , bool   IsBack = false );

    private static PromptResult<T> PromptOrBack<T>( string                       title
                                                  , IEnumerable<PromptOption<T>> options )
    {
        var backOption = new PromptOption<T>(MenuText.Navigation.Back, default!, true);

        var allOptions = options.Append(backOption).ToList();
        var prompt = new SelectionPrompt<PromptOption<T>>().Title(title)
                                                           .UseConverter(o => o.Label)
                                                           .AddChoices(allOptions);
        var selected = AnsiConsole.Prompt(prompt);

        return selected.IsBack
                       ? PromptResult<T>.Back()
                       : PromptResult<T>.Selected(selected.Value);


    }


    public static async Task<T> RunWithSpinnerAsync<T>( string        message
                                                      , Func<Task<T>> action
                                                      , Color?        color = null )
    {
        return await AnsiConsole.Status()
                                .Spinner(Spinner.Known.Dots)
                                .SpinnerStyle(color ?? Color.White)
                                .StartAsync(message
                                          , async _ =>
                                            {
                                                return await action();
                                            });
    }

    public static async Task<T> RunWithSpinnerAsync<T>( string                        message
                                                      , Func<Action<string>, Task<T>> action
                                                      , Color?                        color = null 
        , string messageSuffix = "" )
    {
        var suffix = messageSuffix.HasValue() ? $" - {messageSuffix}" : message;
        
        return await AnsiConsole.Status()
                                .Spinner(new CaseSwappingSpinner(message))
                                .SpinnerStyle(color ?? Color.White)
                                .StartAsync(suffix
                                          , async ctx =>
                                            {
                                                void Report( string text) => ctx.Status(text);
                                                return await action(Report);
                                            });
    }

   /// <summary>
   /// Menu → PromptForX → TrySelect → Execute → DisplayResult → Pause
   /// </summary>
   /// <returns></returns>
    private static async Task<int> RunSpectreMenuAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();

            var header = new FigletText(MenuText.Header.Title).LeftJustified()
                                                              .Color(Color.Cyan1);

            AnsiConsole.Write(header);
            AnsiConsole.MarkupLine(MenuText.Header.Subtitle + "\n");

            var menu = new ConsoleMenu
                       {
                               Title = MenuText.MainMenuTitle
                             , Hint  = MenuText.Navigation.MoreChoicesHint
                       }
                       .Add(MenuText.Commands.BuildComponent.Label
                          , HandleBuildAsync)
                       .Add(MenuText.Commands.DeployComponent.Label
                          , HandleDeployAsync)
                       .Add(MenuText.Commands.VerifyEnvironment.Label
                          , HandleVerifyAsync)
                       .Add(MenuText.Commands.ViewAuditLogs.Label
                          , HandleViewAuditLogsAsync)
                       .Add(MenuText.Commands.ListArtifacts.Label
                          , HandleListArtifactsAsync)
                       .Add(MenuText.Commands.LaunchScalar.Label
                            , HandleLaunchScalar)
                       .AddExit(MenuText.Navigation.Exit);

            var result = await menu.ShowAsync();

            if (result != MenuResult.Exit) continue;

            AnsiConsole.MarkupLine(MenuText.Navigation.Goodby);

            return 0;
        }
    }

    private static async Task HandleLaunchScalar()
    {
        var panel = new Panel(MenuText.Commands
                                      .LaunchScalar
                                      .PanelTitle).BorderColor(MenuText.Commands
                                                                       .LaunchScalar
                                                                       .Color);
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        if (TrySelect(PromptForEnvironment()
                    , out var environment).Not())
            return;
        
        AnsiConsole.MarkupLine($"[green]Launching Scalar in {environment}...[/]");
        
        StartApi(environment);
        await WaitForApiReady(environment);
        
        OpenScalar(environment);

        PauseForUser();
    }

    // private static async Task StartApi(string scriptPath)
    // {
    //     var psi = new ProcessStartInfo
    //               {
    //                       FileName = "cmd.exe"
    //                     , Arguments = @$"/c start "" {scriptPath}\start-api.bat"
    //                     , CreateNoWindow = true
    //                     , UseShellExecute = false
    //                       //   FileName               = "cmd.exe"
    //                       // , Arguments              = $"/c \"{scriptPath}\\start-api.bat\""
    //                       // , WorkingDirectory       = scriptPath
    //                       // , RedirectStandardOutput = true
    //                       // , RedirectStandardError  = true
    //                       // , UseShellExecute        = false
    //                       // , CreateNoWindow         = true
    //               };
    //
    //     using var process = Process.Start(psi);
    //     
    //     // var output = await process!.StandardOutput.ReadToEndAsync();
    //     // var error  = await process.StandardError.ReadToEndAsync();
    //     // var result = new CommandResult(error.HasNoValue()
    //     //                              , output
    //     //                              , error);
    //     // DisplayResult(result);
    //     
    //     //await process.WaitForExitAsync();
    // }
    
    private static void StartApi( Environment environment )
    {
        var scriptFolderPath = $@"{ScriptFolder}{environment}";
        
        var completeScriptPath = Path.Combine(scriptFolderPath
                                         , ScriptFileName);
        if (File.Exists(completeScriptPath)
                .Not())
        {
            AnsiConsole.MarkupLine($"[red]Script not found: {completeScriptPath}[/]");
            return;
        }

        var psi = new ProcessStartInfo
                  {
                          FileName         = "wt.exe"
                        , Arguments        = @$"--profile ""{environment.ToString().ToUpper()}: CP API"""
                        , UseShellExecute  = true
                        , CreateNoWindow   = false
                        , WorkingDirectory = scriptFolderPath
                  };

        try
        {
            Process.Start(psi);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        
    }
    private static async Task WaitForApiReady(Environment environment)
    {
        var healthEndpoint = ApiEnvironments.Url(environment) + "/Scalar";
    
        using var httpClient = new HttpClient();
    
        while (true)
        {
            try
            {
                var response = await httpClient.GetAsync(healthEndpoint);
                if (response.IsSuccessStatusCode) return;
            }
            catch
            {
                // API not ready yet
            }
        
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }

    private static void OpenScalar(Environment environment)
    {
        var scalarUrl = ApiEnvironments.Url(environment) + "/scalar";
        Process.Start(new ProcessStartInfo(scalarUrl) { UseShellExecute = true });
    }
    
    private static async Task HandleBuildAsync()
    {
        var panel = new Panel(MenuText.Commands.BuildComponent.Label).BorderColor(Color.Cyan1);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        if (TrySelect(PromptForComponent()
                    , out var component).Not())
            return;
        
        var buildCommand = new BuildCommand(component
                                          , Environment.All
                                          , _serviceProvider!.GetRequiredService<IArtifactStorage>()
                                          , _serviceProvider!.GetRequiredService<IPowerShellExecutor>()
                                          , _serviceProvider!.GetRequiredService<IAuditLog>()
                                          , _serviceProvider!.GetRequiredService<ILogger<BuildCommand>>());

        var result = await RunWithSpinnerAsync($"Building {component.Name}"
                                             , report => buildCommand.ExecuteAsync(report)
                                             , Color.Cyan
                                             , "For All Environments");

        DisplayResult(result);

        PauseForUser();
    }


    private static async Task HandleDeployAsync()
    {
        var panel = new Panel(MenuText.Commands.DeployComponent.Label).BorderColor(Color.Yellow);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        if (TrySelect(PromptForComponent(isDeploy: true)
                    , out var component).Not())
            return;

        if (TrySelect(PromptForEnvironment()
                    , out var environment).Not())
            return;
        
        if (TrySelect(await PromptForVersion(component
                                           , environment)
                    , out var artifact).Not())
            return;
        
        var deployCommand = new DeployCommand(component
                                            , environment
                                            , _serviceProvider!.GetRequiredService<IArtifactStorage>()
                                            , _serviceProvider!.GetRequiredService<IPowerShellExecutor>()
                                            , _serviceProvider!.GetRequiredService<IAuditLog>()
                                            , _serviceProvider!.GetRequiredService<ILogger<DeployCommand>>()
                                            , scriptsPath: null
                                            , targetVersion: artifact.Version);

        // IMPORTANT: Don't use spinner for deployments!
        // Deployments may need user confirmation (Console.ReadLine)
        // Spectre.Console spinners hijack stdin and prevent ReadLine from working
        AnsiConsole.MarkupLine($"\n[yellow]Deploying {component.Name} to {environment}...[/]\n");
        var result = await deployCommand.ExecuteAsync();

        DisplayResult(result);
        PauseForUser();
    }

    private static async Task HandleVerifyAsync()
    {
        var panel = new Panel(MenuText.Commands.VerifyEnvironment.Label).BorderColor(Color.Green);
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        if (TrySelect(PromptForEnvironment()
                    , out var environment).Not())
            return;

        var verifyCommand = new VerifyCommand(environment
                                            , _serviceProvider!.GetRequiredService<IAuditLog>()
                                            , _serviceProvider!.GetRequiredService<ILogger<VerifyCommand>>()
                                            , _serviceProvider!.GetRequiredService<HttpClient>()
        );

        var result = await RunWithSpinnerAsync($"Verifying {environment} environment..."
                                             , () => verifyCommand.ExecuteAsync()
                                             , Color.Red);

        DisplayResult(result);
        PauseForUser();
    }
    
    private static async Task HandleViewAuditLogsAsync()
    {
        var panel = new Panel("[cyan]RECENT AUDIT LOGS[/]")
                .BorderColor(Color.Cyan1);
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        var auditLog = _serviceProvider!.GetRequiredService<IAuditLog>();
        var entries  = await auditLog.GetRecentEntriesAsync(20);

        if (entries.Any()
                   .Not())
        {
            AnsiConsole.MarkupLine("[yellow]No audit entries found.[/]");
            PauseForUser();
            return;
        }

        var table = new Table().Border(TableBorder.Rounded)
                               .BorderColor(Color.Grey)
                               .AddColumn(new TableColumn("Status").Centered())
                               .AddColumn("Timestamp")
                               .AddColumn("Command")
                               .AddColumn("Component")
                               .AddColumn("Env")
                               .AddColumn("User");

        foreach (var entry in entries)
        {
            var statusMarkup = entry.Success
                                       ? "[green]  ✅   [/]"
                                       : "[red]  ⛔   [/]";
            var envMarkup = entry.Environment?.ToString() ?? "N/A";

            table.AddRow(statusMarkup
                       , entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                       , entry.Command
                       , entry.Component
                       , envMarkup
                       , entry.User
            );
        }

        AnsiConsole.Write(table);
        PauseForUser();
    }

    private static async Task HandleListArtifactsAsync()
    {
        var panel = new Panel(MenuText.Commands.ListArtifacts.Label).BorderColor(Color.Cyan1);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        if (TrySelect(PromptForComponent()
                    , out var component).Not())
            return;

        var storage = _serviceProvider!.GetRequiredService<IArtifactStorage>();
        var artifacts = await storage.GetAllArtifactsAsync(component);
        
        if (artifacts.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No artifacts found for {component.Name}.[/]");
            PauseForUser();
            return;
        }

        var table = new Table().Border(TableBorder.Rounded)
                               .BorderColor(Color.Grey)
                               .AddColumn("Environment")
                               .AddColumn("Version")
                               .AddColumn("Build Time")
                               .AddColumn("Build Machine")
                               .AddColumn("Path");

        foreach (var artifact in artifacts.OrderByDescending(artifact => artifact.Metadata.BuildTimestamp))
        {
            var envColorTuple = GetEnvColor(artifact.Metadata.BuiltFor);

            table.AddRow($"[{envColorTuple.Color}]{envColorTuple.Env}[/]"
                       , artifact.Version
                       , artifact.Metadata.BuildTimestamp.ToString("yyyy-MM-dd HH:mm:ss")
                       , artifact.Metadata.BuildMachine
                       , artifact.Path);
        }

        AnsiConsole.Write(table);
        PauseForUser();
    }


    private static (string Color, string Env) GetEnvColor(Environment environment )
    {

        (string Color, string Env) envColorTuple = environment switch
        {
                Environment.Dev  => ("green", "Dev")
              , Environment.Qa   => ("yellow", "Qa")
              , Environment.Prod => ("red", "Prod")
              , _                => ("grey", "All")
        };

        return envColorTuple;
    }

    private static PromptResult<Component> PromptForComponent( bool isDeploy = false )
    {
        var options = new List<PromptOption<Component>>
                      {
                              new(MenuText.ComponentMenu.Api
                                , Component.CpApi)
                            , new(MenuText.ComponentMenu.LocalAiAssistant
                                , Component.LaaMauiApp)
                      };

        if (!isDeploy)
        {
            options.AddRange(new[]
                             {
                                     new PromptOption<Component>("📦  All NuGets"
                                                               , Component.AllNugetComponents)
                             });
        }

        return PromptOrBack(MenuText.Prompts.SelectComponent
                          , options);
    }

    
    private static PromptResult<Environment> PromptForEnvironment(bool allowProd = true)
    {
        var options = new List<PromptOption<Environment>>
                      {
                              new(MenuText.Environments.Dev, Environment.Dev),
                              new(MenuText.Environments.Qa, Environment.Qa)
                      };

        if (allowProd)
            options.Add(new PromptOption<Environment>(MenuText.Environments.Prod
                                                    , Environment.Prod));

        return PromptOrBack(MenuText.Prompts.SelectEnvironment, options);
    }

    
    private static async Task<PromptResult<Artifact>> PromptForVersion( Component   component
                                                                      , Environment environment
                                                                      , int         numberOfArtifacts = 5 )
    {
        var artifactStorage = _serviceProvider!.GetRequiredService<IArtifactStorage>();

        var artifacts = await artifactStorage.GetMostRecentArtifactsAsync(component
                                                                        , environment
                                                                        , numberOfArtifacts);
        var availableArtifacts = artifacts.ToList();
        var options = availableArtifacts.Select(artifact => new PromptOption<Artifact>($"{artifact.Version} - {artifact.Metadata.BuiltFor}"
                                                                                     , artifact)).ToList();

        return PromptOrBack(MenuText.Prompts.SelectVersion
                          , options);
    }

    private static void PauseForUser()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Markup(MenuText.Navigation.PressAnyKey);

        Console.ReadKey(true);
    }


    private static void DisplayResult( CommandResult result )
    {
        AnsiConsole.WriteLine();

        if (result.Success)
        {
            var successPanel = new Panel($"[green]{MenuText.Results.Success} [/]\n\n{result.Message.EscapeMarkup()}").BorderColor(Color.Green);
            AnsiConsole.Write(successPanel);
        }
        else
        {
            var innerPanelText = result.Message.HasValue()
                                         ? $"\n\n{result.Message.EscapeMarkup()}"
                                         : string.Empty;

            var failurePanel = new Panel($"[red]{MenuText.Results.Failure}[/]{innerPanelText}").BorderColor(Color.Red);
                                                                                              // .Padding(1, 1);
            AnsiConsole.Write(failurePanel);

            if (result.ErrorDetails?.HasValue() ?? false)
            {
                AnsiConsole.MarkupLine("\n[yellow]Details:[/]");
                AnsiConsole.MarkupLine($"[grey]{result.ErrorDetails.EscapeMarkup()}[/]");
            }
        }

        AnsiConsole.WriteLine();
    }

    private static void DisplayCommandLineResult( CommandResult result )
    {
        Console.WriteLine();
        Console.WriteLine(result.Success
                                  ? MenuText.Results.Success
                                  : MenuText.Results.Failure);
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
        var componentArg = new Argument<string>("component");
        componentArg.Description = $"Component to build";

        var envOption = new Option<string>("--env"
                                         , "Target environment (dev, qa, prod)")
                        {
                                IsRequired = true
                        };

        var command = new Command("build"
                                , "Build a component and create a versioned artifact")
                      {
                              componentArg, envOption
                      };

        command.SetHandler(async ( componentName
                                 , envName ) =>
                           {
                               try
                               {
                                   var component   = Component.FromString(componentName);
                                   var environment = ParseEnvironment(envName);

                                   // Note: BuildCommand constructor handles environment restrictions per component type

                                   var buildCommand = new BuildCommand(component
                                                                     , environment
                                                                     , serviceProvider.GetRequiredService<IArtifactStorage>()
                                                                     , serviceProvider.GetRequiredService<IPowerShellExecutor>()
                                                                     , serviceProvider.GetRequiredService<IAuditLog>()
                                                                     , serviceProvider.GetRequiredService<ILogger<BuildCommand>>()
                                   );

                                   var result = await buildCommand.ExecuteAsync();
                                   DisplayResult(result);
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
        var componentArg = new Argument<string>("component"
                                              , "Component to deploy"
        );

        var envOption = new Option<string>(
                            "--env"
                          , "Target environment (dev, qa, prod)"
                        ) { IsRequired = true };

        var versionOption = new Option<string?>(
                                "--version"
                              , "Specific version to deploy (optional - defaults to latest)"
                            ) { IsRequired = false };

        var command = new Command("deploy"
                                , "Deploy an artifact to an environment")
                      {
                              componentArg
                            , envOption
                            , versionOption
                      };

        command.SetHandler(async ( componentName
                                 , envName
                                 , version ) =>
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
                                                                       , scriptsPath: null
                                                                       , targetVersion: version
                                   );

                                   var result = await deployCommand.ExecuteAsync();
                                   DisplayResult(result);
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
        var envOption = new Option<string>("--env"
                                         , "Environment to verify (dev, qa, prod)")
                        { IsRequired = true };

        var command = new Command("verify"
                                , "Verify environment health")
                      { envOption };

        command.SetHandler(async ( envName ) =>
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

    private static bool TrySelect<T>( PromptResult<T> result
                                    , out T           value )
    {
        if (result.Kind != PromptResultKind.Selected)
        {
            value = default!;
            return false;
        }

        value = result.Value!;
        return true;
    }


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
        services.AddTransient<IPowerShellExecutor>(sp => new PowerShellExecutor(ScriptOutputMode.ErrorsOnly));
        services.AddSingleton<IVersionSelector, ConsoleVersionSelector>();
        services.AddSingleton<IDeploymentStateService, JsonDeploymentStateService>();
        services.AddSingleton<HttpClient>();

        return services;
    }
    
    public static async Task ShowAllSpinnersAsync()
    {
        // 1. Get the "Known" spinners via Reflection
        var knownSpinners = typeof(Spinner.Known).GetProperties(BindingFlags.Public
                                                              | BindingFlags.Static)
                                                 .Where(propertyInfo => propertyInfo.PropertyType == typeof(Spinner))
                                                 .Select(propertyInfo => new
                                                                         {
                                                                                 Name    = propertyInfo.Name
                                                                               , Spinner = (Spinner)propertyInfo.GetValue(null)!
                                                                         });

        // 2. Create your custom spinner instance
        var custom = new
                     {
                             Name    = "CustomCaseSwapper"
                           , Spinner = (Spinner)new CaseSwappingSpinner("Thinking")
                     };

        // 3. Combine them (Custom first, then Known)
        var allSpinners = new[] { custom }.Concat(knownSpinners);

        foreach (var item in allSpinners)
        {
            await AnsiConsole.Status()
                             .Spinner(item.Spinner)
                             .SpinnerStyle(Style.Parse("cyan"))
                             .StartAsync($"Spinner: {item.Name.EscapeMarkup()}"
                                       , async _ =>
                                         {
                                             await Task.Delay(20000);
                                         });
        }

        AnsiConsole.MarkupLine("\n[grey]Done previewing spinners. Press any key…[/]");
        Console.ReadKey();
    }

}