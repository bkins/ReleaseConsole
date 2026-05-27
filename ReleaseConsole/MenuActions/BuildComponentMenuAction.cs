using CP.Client.Core.Avails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReleaseConsole.Commands;
using ReleaseConsole.ConsoleUi;
using ReleaseConsole.Menu;
using ReleaseConsole.Services.Interfaces;
using Spectre.Console;
using static ReleaseConsole.Menu.MenuText.Commands;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.MenuActions;

public sealed class BuildComponentMenuAction : ConsoleMenuActionBase
{
    public BuildComponentMenuAction( IConsolePromptService     prompts
                                   , ICommandExecutionPipeline runner
                                   , IServiceProvider          services )
        : base(prompts, runner, services) { }

    public override string Label => BuildComponent.Label;
    public override string Path  => "build";
    public override int    Order => 10;

    public override async Task ExecuteAsync()
    {
        Runner.WritePanel(BuildComponent.Label, Color.Cyan1);

        if (TrySelect(Prompts.SelectComponent(), out var component).Not())
            return;

        var buildCommand = new BuildCommand(component
                                          , Environment.All
                                          , Services.GetRequiredService<IArtifactStorage>()
                                          , Services.GetRequiredService<IPowerShellExecutor>()
                                          , Services.GetRequiredService<IAuditLog>()
                                          , Services.GetRequiredService<ILogger<BuildCommand>>());

        var result = await Runner.RunAsync($"Building {component.Name}"
                                         , report => buildCommand.ExecuteAsync(report)
                                         , Color.Cyan
                                         , "For All Environments");

        Runner.DisplayResult(result);
        Runner.PauseForUser();
    }
}
