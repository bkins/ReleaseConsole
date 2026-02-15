// Program.cs - Fixed Spectre.Console Version
// Fixes: 1) Uses ASCII symbols instead of emojis, 2) No spinner during PowerShell execution

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReleaseConsole.Commands;
using ReleaseConsole.Core;
using ReleaseConsole.Services;
using Spectre.Console;
using System.CommandLine;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using CP.Client.Core.Avails;
using ReleaseConsole.Core.Spinners;
using ReleaseConsole.Services.Interfaces;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole;

public class Program
{
    private static string?          _version;
    private static ServiceProvider? _serviceProvider;

    public static async Task<int> Main( string[] args )
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
       
        // await ShowAllSpinnersAsync();
        
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

    private static PromptResult<T> PromptOrBack<T>( string           message
                                   , Func<string, T?> parser
                                   , bool             required = true )
    {
        AnsiConsole.MarkupLine($"{message} ([grey]type 'back' to go back[/])");

        var input = AnsiConsole.Prompt(new TextPrompt<string>("> ").AllowEmpty()).Trim();

        if (input.Equals("back"
                       , StringComparison.OrdinalIgnoreCase))
            return new PromptResult<T>(PromptResultKind.Back);

        if (required.Not()
         && input.HasNoValue())
            return new PromptResult<T>(PromptResultKind.Cancel);

        var parsed = parser(input);
        if (parsed is not null)
            return new PromptResult<T>(PromptResultKind.Success
                                     , parsed);
        
        AnsiConsole.MarkupLine("[red]Invalid input.[/]");
        
        return PromptOrBack(message, parser, required);
    }

    private static PromptResult<T> PromptOrBack<T>( string                 message
                                                  , IDictionary<string, T> options )
    {
        
        
        var choices = options.Keys
                             .Append("⬅️ Back")
                             .ToArray();

        var selection = AnsiConsole.Prompt(new SelectionPrompt<string>().Title(message)
                                                                        .AddChoices(choices));

        if (selection == "⬅️ Back")
            return new PromptResult<T>(PromptResultKind.Back);

        return new PromptResult<T>(PromptResultKind.Success
                                 , options[selection]);
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
                                                      , Color?                        color = null )
    {
        return await AnsiConsole.Status()
                                .Spinner(new CaseSwappingSpinner(message))
                                .SpinnerStyle(color ?? Color.White)
                                .StartAsync(message
                                          , async ctx =>
                                            {
                                                void Report( string text ) => ctx.Status(text);
                                                return await action(Report);
                                            });
    }

    public static async Task ShowAllSpinnersAsync()
    {
        // 1. Get the "Known" spinners via Reflection
        var knownSpinners = typeof(Spinner.Known)
                            .GetProperties(BindingFlags.Public | BindingFlags.Static)
                            .Where(p => p.PropertyType == typeof(Spinner))
                            .Select(p => new { Name = p.Name, Spinner = (Spinner)p.GetValue(null)! });

        // 2. Create your custom spinner instance
        var custom = new { Name = "CustomCaseSwapper", Spinner = (Spinner)new CaseSwappingSpinner("Thinking") };

        // 3. Combine them (Custom first, then Known)
        var allSpinners = new[] { custom }.Concat(knownSpinners);

        foreach (var item in allSpinners)
        {
            await AnsiConsole.Status()
                             .Spinner(item.Spinner)
                             .SpinnerStyle(Style.Parse("cyan"))
                             .StartAsync($"Spinner: {item.Name.EscapeMarkup()}", async _ =>
                             {
                                 await Task.Delay(20000); 
                             });
        }

        AnsiConsole.MarkupLine("\n[grey]Done previewing spinners. Press any key…[/]");
        Console.ReadKey();
    }
    
    private static async Task<int> RunSpectreMenuAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();

            // Draw fancy header with ASCII-safe figlet
            var header = new FigletText("Release Console").LeftJustified()
                                                          .Color(Color.Cyan1);
            AnsiConsole.Write(header);

            AnsiConsole.MarkupLine("[dim]Deliberate. Audited. Safe.[/]\n");

            const string buildComponent      = "🔨 Build Component";
            const string deployComponent     = "🚀 Deploy Component";
            const string promoteToProduction = "⬆️ Promote to Production";
            const string verifyEnvironment   = "✅  Verify Environment";
            const string rollbackProduction  = "↩️ Rollback Production";
            const string viewAuditLogs       = "📋 View Audit Logs";
            const string listArtifacts       = "📦 List Artifacts";
            
            // const string fixAuditLogs        = "🛠️ Fix Audit Logs";
            const string exit                = "❌  Exit";

            // Create menu with ASCII symbols instead of emojis
            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("[cyan]What would you like to do?[/]")
                                                                         .PageSize(10)
                                                                         .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
                                                                         .AddChoices(buildComponent
                                                                                   , deployComponent
                                                                                   , promoteToProduction
                                                                                   , verifyEnvironment
                                                                                   , rollbackProduction
                                                                                   , viewAuditLogs
                                                                                   , listArtifacts
                                                                                   // , fixAuditLogs
                                                                                   , exit));

            try
            {
                switch (choice)
                {
                    case buildComponent:
                        await HandleBuildAsync();
                        break;
                    case deployComponent:
                        await HandleDeployAsync();
                        break;
                    case promoteToProduction:
                        await HandlePromoteAsync();
                        break;
                    case verifyEnvironment:
                        await HandleVerifyAsync();
                        break;
                    case rollbackProduction:
                        await HandleRollbackAsync();
                        break;
                    case viewAuditLogs:
                        await HandleViewAuditLogsAsync();
                        break;
                    case listArtifacts:
                        await HandleListArtifactsAsync();
                        break;
                    // case fixAuditLogs:
                    //     FileAuditLog.FixAuditLog();
                    //     break;
                    case exit:
                        AnsiConsole.MarkupLine("\n[cyan]Goodbye![/]");
                        return 0;
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"\n[red]⛔  ERROR: {ex.Message.EscapeMarkup()}[/]");
                AnsiConsole.MarkupLine($"[grey]{ex.StackTrace?.EscapeMarkup()}[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.Markup("[grey]Press any key to continue...[/]");
                Console.ReadKey(true);
            }
        }
    }

    private static async Task HandleBuildAsync()
    {
        var panel = new Panel("[cyan]BUILD COMPONENT[/]").BorderColor(Color.Cyan1);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        if (TrySelect(PromptForComponent(), out var component).Not())
            return;
        var environment = Environment.Dev;

        if (component.Name.IsNotEqualTo("laa",                StringComparison.CurrentCultureIgnoreCase)
         && component.Name.IsNotEqualTo("CpSharedPrimitives", StringComparison.CurrentCultureIgnoreCase)
         && component.Name.IsNotEqualTo("CpClientCore",       StringComparison.CurrentCultureIgnoreCase)
         && component.Name.IsNotEqualTo("AllNugetComponents", StringComparison.CurrentCultureIgnoreCase))
        {
            if (TrySelect(PromptForEnvironment()
                        , out environment).Not())
            {
                return;
            }
        }

        if (environment == Environment.Prod)
        {
            AnsiConsole.MarkupLine("[red]X Cannot build directly for Prod. Use 'Promote' instead.[/]");
            PauseForUser();
            return;
        }

        var buildCommand = new BuildCommand(component
                                          , environment
                                          , _serviceProvider!.GetRequiredService<IArtifactStorage>()
                                          , _serviceProvider!.GetRequiredService<IPowerShellExecutor>()
                                          , _serviceProvider!.GetRequiredService<IAuditLog>()
                                          , _serviceProvider!.GetRequiredService<ILogger<BuildCommand>>());

         var result = await RunWithSpinnerAsync($"Building {component.Name}"
                                             , report => buildCommand.ExecuteAsync(report)
                                             , Color.Red);

        
        DisplayResult(result);

        PauseForUser();
    }

    private static async Task HandleDeployAsync()
    {
        var panel = new Panel("[yellow]DEPLOY COMPONENT[/]").BorderColor(Color.Yellow);
        
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        if (TrySelect(PromptForComponent(isDeploy: true)
                    , out var component).Not())
            return;

        if (TrySelect(PromptForEnvironment(), out var environment).Not())
            return;
        
        
        if (TrySelect(await PromptForVersion(component, environment), out var artifact).Not())
            return;
        

        var deployCommand = new DeployCommand(component
                                            , environment
                                            , _serviceProvider!.GetRequiredService<IArtifactStorage>()
                                            , _serviceProvider!.GetRequiredService<IPowerShellExecutor>()
                                            , _serviceProvider!.GetRequiredService<IDeploymentStateService>()
                                            , _serviceProvider!.GetRequiredService<IAuditLog>()
                                            , _serviceProvider!.GetRequiredService<ILogger<DeployCommand>>()
                                            , scriptsPath: null
                                            , force: false
                                            , targetVersion: artifact.Version
                                            , versionSelector: _serviceProvider!.GetService<IVersionSelector>());

        // IMPORTANT: Don't use spinner for deployments!
        // Deployments may need user confirmation (Console.ReadLine)
        // Spectre.Console spinners hijack stdin and prevent ReadLine from working
        AnsiConsole.MarkupLine($"\n[yellow]Deploying {component.Name} to {environment}...[/]\n");
        var result = await deployCommand.ExecuteAsync();

        DisplayResult(result);
        PauseForUser();
    }
    
    private static async Task HandlePromoteAsync()
    {
        var panel = new Panel("[red]PROMOTE TO PRODUCTION[/]").BorderColor(Color.Red);
        
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
        
        var  component = PromptForComponent();
        switch (component.Kind)
        {
            case PromptResultKind.Back:
            case PromptResultKind.Cancel:
                return; // go back ONE menu level or bubble higher if appropriate

            case PromptResultKind.Success:
                break; // continue
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (TrySelect(PromptOrBack("[yellow]Justification (required):[/]"
                                 , input => input.HasValue()
                                                    ? input
                                                    : null)
                    , out var justification).Not())
        {
            return;
        }


        var promoteCommand = new PromoteCommand(component.Value!
                                              , justification
                                              , _serviceProvider!.GetRequiredService<IArtifactStorage>()
                                              , _serviceProvider!.GetRequiredService<IAuditLog>()
                                              , _serviceProvider!.GetRequiredService<ILogger<PromoteCommand>>()
        );
        
        // AnsiConsole.MarkupLine($"\n[red]Promoting {component.Name} to Production...[/]\n");
        // var result = await promoteCommand.ExecuteAsync();

        var result = await RunWithSpinnerAsync($"Promoting {component.Value!.Name} to Production..."
                                             , () => promoteCommand.ExecuteAsync()
                                             , Color.Red);

        DisplayResult(result);
        PauseForUser();
    }

    private static async Task HandleVerifyAsync()
    {
        var panel = new Panel("[green]VERIFY ENVIRONMENT[/]")
                
                .BorderColor(Color.Green);
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        if (TrySelect(PromptForEnvironment(), out var environment).Not())
            return;
        
        var verifyCommand = new VerifyCommand(environment
                                            , _serviceProvider!.GetRequiredService<IAuditLog>()
                                            , _serviceProvider!.GetRequiredService<ILogger<VerifyCommand>>()
                                            , _serviceProvider!.GetRequiredService<HttpClient>()
        );

        // AnsiConsole.MarkupLine($"\n[green]Verifying {environment} environment...[/]\n");
        // var result = await verifyCommand.ExecuteAsync();
        
        var result = await RunWithSpinnerAsync($"Verifying {environment} environment..."
                                             , () => verifyCommand.ExecuteAsync()
                                             , Color.Red);
        
        DisplayResult(result);
        PauseForUser();
    }

    private static async Task HandleRollbackAsync()
    {
        var panel = new Panel("[red]ROLLBACK PRODUCTION[/]").BorderColor(Color.Red);
        
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        if (TrySelect(PromptForComponent(), out var component))
            return;
        
        var version = AnsiConsole.Prompt(new TextPrompt<string>("[yellow]Target version to rollback to:[/]").PromptStyle("cyan")
                                                                                                            .ValidationErrorMessage("[red]Version cannot be empty[/]")
                                                                                                            .Validate(v => !string.IsNullOrWhiteSpace(v)));
        var rollbackCommand = new RollbackCommand(component
                                                , version
                                                , _serviceProvider!.GetRequiredService<IArtifactStorage>()
                                                , _serviceProvider!.GetRequiredService<IPowerShellExecutor>()
                                                , _serviceProvider!.GetRequiredService<IAuditLog>()
                                                , _serviceProvider!.GetRequiredService<ILogger<RollbackCommand>>());

        // AnsiConsole.MarkupLine($"\n[red]Rolling back {component.Name}...[/]\n");
        // var result = await rollbackCommand.ExecuteAsync();

        var result = await RunWithSpinnerAsync($"Rolling back {component.Name}..."
                                             , () => rollbackCommand.ExecuteAsync()
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
                                       :   "[red]  ⛔   [/]";
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
        var panel = new Panel("[cyan]LIST ARTIFACTS[/]").BorderColor(Color.Cyan1);
        
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        if (TrySelect(PromptForComponent()
                    , out var component).Not())
            return;
        
        var storage   = _serviceProvider!.GetRequiredService<IArtifactStorage>();
        var artifacts = await storage.GetAllArtifactsAsync(component,    Environment.Dev);
        artifacts.AddRange(await storage.GetAllArtifactsAsync(component, Environment.Qa));
        artifacts.AddRange(await storage.GetAllArtifactsAsync(component, Environment.Prod));
        
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

        foreach (var artifact in artifacts.OrderByDescending(a => a.Metadata.BuildTimestamp))
        {
            var envColorTuple = GetEnvColor(artifact.Path);
            
            table.AddRow($"[{envColorTuple.Color}]{envColorTuple.Env}[/]"
                       , artifact.Version
                       , artifact.Metadata.BuildTimestamp.ToString("yyyy-MM-dd HH:mm:ss")
                       , artifact.Metadata.BuildMachine
                       , artifact.Path);
        }

        AnsiConsole.Write(table);
        PauseForUser();
    }

    
    private static (string Color, string Env) GetEnvColor( string artifactPath )
    {
        var lowerPath = artifactPath.ToLower();
        var isDev     = lowerPath.EndsWith("dev.zip");
        var isQa      = lowerPath.EndsWith("qa.zip");
        var isProd    = lowerPath.EndsWith("prod.zip");

        (string Color, string Env) envColorTuple = ("", "");

        if (isDev)
            envColorTuple = ("green", "Dev");
        else if (isQa)
            envColorTuple = ("yellow", "Qa");
        else if (isProd)
            envColorTuple = ("red", "Prod");
        else
            envColorTuple = ("grey", "All");
        
        return envColorTuple;
    }

    private static PromptResult<Component> PromptForComponent(bool isDeploy = false)
    {
        if (isDeploy)
        {
            return PromptOrBack("[cyan]Select component:[/]"
                              , new Dictionary<string, Component>
                                {
                                        { "🧠  API (CpApi)", Component.CpApi }
                                      , { "📱  LocalAiAssistant (Laa) -> All Envs", Component.LaaMauiApp }
                                });    
        }
        return PromptOrBack("[cyan]Select component:[/]"
                          , new Dictionary<string, Component>
                            {
                                    { "🧠  API (CpApi)", Component.CpApi }
                                  , { "📱  LocalAiAssistant (Laa) -> All Envs", Component.LaaMauiApp }
                                  , { "🛠️ CpSharedPrimitives", Component.CpSharedPrimitives }
                                  , { "⚗️  CpClientCore", Component.CpClientCore }
                                  , { "📦  All NuGets", Component.AllNugetComponents }
                            });
    }

    private static async Task<PromptResult<Artifact>> PromptForVersion( Component   component
                                                                      , Environment environment
                                                                      , int         numberOfArtifacts = 5 )
    {
        var artifactStorage = new LocalArtifactStorage(_serviceProvider!.GetRequiredService<ILogger<LocalArtifactStorage>>());
        var artifacts = await artifactStorage.GetMostRecentArtifactsAsync(component
                                                                        , environment
                                                                        , numberOfArtifacts);
        var artifactMenuItems = artifacts.ToDictionary(artifact => $"{artifact.Version} - {artifact.Metadata.BuiltFor}");
        
        return PromptOrBack("[cyan]Select version:[/]"
                          , artifactMenuItems);
    }
    
    private static PromptResult<Environment> PromptForEnvironment( bool allowProd = true )
    {
        var choices = new Dictionary<string, Environment>
                      {
                              { "🔨 Dev", Environment.Dev }
                            , { "🧪 QA", Environment.Qa }
                      };

        if (allowProd) choices.Add("🚀 Prod", Environment.Prod);

        return PromptOrBack("[cyan]Select environment:[/]"
                          , choices);
    }
    
    private static void PauseForUser()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Markup("[grey]Press any key to continue...[/]");
        
        Console.ReadKey(true);
    }

    
    private static void DisplayResult( CommandResult result )
    {
        AnsiConsole.WriteLine();

        if (result.Success)
        {
            var successPanel = new Panel($"[green]✅  SUCCESS [/]\n\n{result.Message
                                                                           .EscapeMarkup()}").BorderColor(Color.Green)
                                                                                             //.Padding(1, 1)
                                                                                             ;
            AnsiConsole.Write(successPanel);
        }
        else
        {
            var failurePanel = new Panel($"[red]⛔  FAILURE[/]\n\n{result.Message
                                                                        .EscapeMarkup()}").BorderColor(Color.Red)
                                                                                                  .Padding(1, 1);
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
                                  ? "✅  SUCCESS "
                                  : "⛔ FAILURE");
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
        rootCommand.Add(BuildPromoteCommand(serviceProvider));
        rootCommand.Add(BuildVerifyCommand(serviceProvider));
        rootCommand.Add(BuildRollbackCommand(serviceProvider));

        return rootCommand;
    }

    private static Command BuildBuildCommand( ServiceProvider serviceProvider )
    {
        var componentArg = new Argument<string>("component");
        componentArg.Description = $"Component to build (acceptable component types: '{nameof(Component.CpApi)}', '{nameof(Component.LaaMauiApp)}' or \"maui\", `{nameof(Component.CpSharedPrimitives)} or \"lib1\"`, or '{nameof(Component.CpClientCore)} or \"lib2\"' )\"";

        var envOption = new Option<string>("--env"
                                         , "Target environment (dev, qa)")
                        {
                                IsRequired = true
                        };

        var command = new Command("build"
                                , "Build a component and create a versioned artifact")
                      {
                              componentArg
                            , envOption
                      };

        command.SetHandler(async ( componentName, envName ) =>
                           {
                               try
                               {
                                   var component   = Component.FromString(componentName);
                                   var environment = ParseEnvironment(envName);

                                   if (environment == Environment.Prod)
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

                                   // AnsiConsole.MarkupLine($"\n[cyan]Building {component.Name} for {environment}...[/]\n");
                                   // var result = await buildCommand.ExecuteAsync();
                                  
                                   
                                   var result = await RunWithSpinnerAsync($"Building {component.Name}..."
                                                                        , () => buildCommand.ExecuteAsync()
                                                                        , Color.Cyan1);

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
        var componentArg = new Argument<string>("component"
                                              , "Component to deploy");

        var envOption = new Option<string>("--env"
                                         , "Target environment (dev, qa, prod)") 
                        { IsRequired = true };

        var versionOption = new Option<string?>(  // ← NEW
                                "--version",
                                "Specific version to deploy (optional, defaults to latest)"
                            ) { IsRequired = false };

        var forceOption = new Option<bool>(  // ← NEW
                              "--force",
                              "Skip confirmation prompts"
                          ) { IsRequired = false };

        var command = new Command("deploy"
                                , "Deploy the latest artifact to an environment")
                      {
                              componentArg
                            , envOption
                            , versionOption
                            , forceOption
                      };

        command.SetHandler(async (componentName, envName, version, force) =>
                           {
                               try
                               {
                                   var component   = Component.FromString(componentName);
                                   var environment = ParseEnvironment(envName);

                                   var deployCommand = new DeployCommand(component
                                                                       , environment
                                                                       , serviceProvider.GetRequiredService<IArtifactStorage>()
                                                                       , serviceProvider.GetRequiredService<IPowerShellExecutor>()
                                                                       , serviceProvider.GetRequiredService<IDeploymentStateService>()
                                                                       , serviceProvider.GetRequiredService<IAuditLog>()
                                                                       , serviceProvider.GetRequiredService<ILogger<DeployCommand>>()
                                                                       , scriptsPath: null
                                                                       , force: force
                                                                       , targetVersion: version
                                                                       , versionSelector: serviceProvider.GetService<IVersionSelector>());

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
                         , versionOption
                         , forceOption);

        return command;
    }

    private static Command BuildPromoteCommand( ServiceProvider serviceProvider )
    {
        var componentArg = new Argument<string>("component"
                                              , "Component to promote");

        var justificationOption = new Option<string>("--reason"
                                                   , "Justification for production promotion")
                                  { IsRequired = true };

        var command = new Command("promote"
                                , "Promote a QA artifact to Production")
                      {
                              componentArg
                            , justificationOption
                      };

        command.SetHandler(async ( componentName
                                 , justification ) =>
                           {
                               try
                               {
                                   var component = Component.FromString(componentName);

                                   var promoteCommand = new PromoteCommand(component
                                                                         , justification
                                                                         , serviceProvider.GetRequiredService<IArtifactStorage>()
                                                                         , serviceProvider.GetRequiredService<IAuditLog>()
                                                                         , serviceProvider.GetRequiredService<ILogger<PromoteCommand>>());

                                   var result = await promoteCommand.ExecuteAsync();
                                   DisplayCommandLineResult(result);
                               }
                               catch (Exception ex)
                               {
                                   Console.WriteLine($"ERROR: {ex.Message}");
                               }
                           }
                         , componentArg
                         , justificationOption);

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

    private static Command BuildRollbackCommand( ServiceProvider serviceProvider )
    {
        var componentArg = new Argument<string>("component"
                                              , "Component to rollback");

        var versionOption = new Option<string>("--to"
                                             , "Target version to rollback to")
                            { IsRequired = true };

        var command = new Command("rollback"
                                , "Rollback to a previous production version")
                      {
                              componentArg
                            , versionOption
                      };

        command.SetHandler(async ( componentName
                                 , version ) =>
                           {
                               try
                               {
                                   var component = Component.FromString(componentName);

                                   var rollbackCommand = new RollbackCommand(component
                                                                           , version
                                                                           , serviceProvider.GetRequiredService<IArtifactStorage>()
                                                                           , serviceProvider.GetRequiredService<IPowerShellExecutor>()
                                                                           , serviceProvider.GetRequiredService<IAuditLog>()
                                                                           , serviceProvider.GetRequiredService<ILogger<RollbackCommand>>()
                                   );

                                   var result = await rollbackCommand.ExecuteAsync();
                                   DisplayCommandLineResult(result);
                               }
                               catch (Exception ex)
                               {
                                   Console.WriteLine($"ERROR: {ex.Message}");
                               }
                           }
                         , componentArg
                         , versionOption);

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
        if (result.Kind != PromptResultKind.Success)
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

}