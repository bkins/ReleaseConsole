using CP.Client.Core.Avails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReleaseConsole.Commands;
using ReleaseConsole.ConsoleUi;
using ReleaseConsole.Menu;
using ReleaseConsole.Services.Interfaces;
using static ReleaseConsole.Menu.MenuText.Commands;

namespace ReleaseConsole.MenuActions;

public sealed class DbActionsMenuAction : ConsoleMenuActionBase
{
    public DbActionsMenuAction( IConsolePromptService     prompts
                              , ICommandExecutionPipeline runner
                              , IServiceProvider          services )
        : base(prompts, runner, services) { }

    public override string Label => DbAction.Label;
    public override string Path  => "db";

    public override async Task ExecuteAsync()
    {
        Runner.WritePanel(DbAction.PanelTitle, DbAction.Color);

        if (TrySelect(Prompts.SelectDbAction(), out var action).Not())
            return;

        var swapDbCommand = new SwapDbCommand(Services.GetRequiredService<IAuditLog>()
                                            , Services.GetRequiredService<ILogger<BuildCommand>>()
                                            , Services.GetRequiredService<IPowerShellExecutor>()
                                            , action);

        var result = await Runner.RunAsync(swapDbCommand.SpinnerText
                                         , swapDbCommand.ExecuteAsync
                                         , swapDbCommand.SpinnerColor);

        Runner.DisplayResult(result);
        Runner.PauseForUser();
    }
}
