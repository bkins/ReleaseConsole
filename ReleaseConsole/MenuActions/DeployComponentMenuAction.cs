using CP.Client.Core.Avails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReleaseConsole.Commands;
using ReleaseConsole.ConsoleUi;
using ReleaseConsole.Menu;
using ReleaseConsole.Services.Interfaces;
using Spectre.Console;
using static ReleaseConsole.Menu.MenuText.Commands;

namespace ReleaseConsole.MenuActions;

[MenuAction(path: "deploy", label: "🚀 Deploy Component", Order = 20)]
public sealed class DeployComponentMenuAction : ConsoleMenuActionBase
{
    public DeployComponentMenuAction( IConsolePromptService     prompts
                                    , ICommandExecutionPipeline runner
                                    , IServiceProvider          services )
        : base(prompts, runner, services) { }

    public override async Task ExecuteAsync()
    {
        Runner.WritePanel(DeployComponent.Label, Color.Yellow);

        if (TrySelect(Prompts.SelectComponent(isDeploy: true), out var component).Not())
            return;

        if (TrySelect(Prompts.SelectEnvironment(), out var environment).Not())
            return;

        if (TrySelect(await Prompts.SelectArtifactAsync(component, environment), out var artifact).Not())
            return;

        var deployCommand = new DeployCommand(component
                                            , environment
                                            , Services.GetRequiredService<IArtifactStorage>()
                                            , Services.GetRequiredService<IPowerShellExecutor>()
                                            , Services.GetRequiredService<IAuditLog>()
                                            , Services.GetRequiredService<ILogger<DeployCommand>>()
                                            , scriptsPath:   null
                                            , targetVersion: artifact.Version);

        // Spinner must NOT be used during deploy — it hijacks stdin and blocks Console.ReadLine prompts
        AnsiConsole.MarkupLine($"\n[yellow]Deploying {component.Name} to {environment}...[/]\n");
        var result = await deployCommand.ExecuteAsync();

        Runner.DisplayResult(result);
        Runner.PauseForUser();
    }
}
