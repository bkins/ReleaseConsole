using CP.Client.Core.Avails;
using Microsoft.Extensions.DependencyInjection;
using ReleaseConsole.ConsoleUi;
using ReleaseConsole.Core;
using ReleaseConsole.Menu;
using ReleaseConsole.Services.Interfaces;
using Spectre.Console;
using static ReleaseConsole.Menu.MenuText.Commands;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.MenuActions;

[MenuAction(path: "artifacts", label: "📦 List Artifacts", Order = 50)]
public sealed class ListArtifactsMenuAction : ConsoleMenuActionBase
{
    public ListArtifactsMenuAction( IConsolePromptService     prompts
                                  , ICommandExecutionPipeline runner
                                  , IServiceProvider          services )
        : base(prompts, runner, services) { }

    public override async Task ExecuteAsync()
    {
        Runner.WritePanel(ListArtifacts.Label, Color.Cyan1);

        if (TrySelect(Prompts.SelectComponent(), out var component).Not())
            return;

        var storage   = Services.GetRequiredService<IArtifactStorage>();
        var artifacts = await storage.GetAllArtifactsAsync(component);

        if (artifacts.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No artifacts found for {component.Name}.[/]");
            Runner.PauseForUser();
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
        Runner.PauseForUser();
    }

    private static (string Color, string Env) GetEnvColor(Environment environment) =>
        environment switch
        {
                Environment.Dev  => ("green",  "Dev")
              , Environment.Qa   => ("yellow", "Qa")
              , Environment.Prod => ("red",    "Prod")
              , _                => ("grey",   "All")
        };
}
