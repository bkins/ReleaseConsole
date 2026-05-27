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

public sealed class VerifyEnvironmentMenuAction : ConsoleMenuActionBase
{
    private readonly ILogger<VerifyEnvironmentMenuAction> _logger;
    private readonly HttpClient                           _httpClient;

    public VerifyEnvironmentMenuAction( IConsolePromptService                  prompts
                                      , ICommandExecutionPipeline              runner
                                      , IServiceProvider                       services
                                      , ILogger<VerifyEnvironmentMenuAction>   logger
                                      , HttpClient                             httpClient )
        : base(prompts, runner, services)
    {
        _logger     = logger;
        _httpClient = httpClient;
    }

    public override string Label => VerifyEnvironment.Label;
    public override string Path  => "verify";
    public override int    Order => 30;

    public override async Task ExecuteAsync()
    {
        Runner.WritePanel(VerifyEnvironment.Label, Color.Green);

        if (TrySelect(Prompts.SelectEnvironment(), out var environment).Not())
            return;

        var verifyCommand = new VerifyCommand(environment
                                            , Services.GetRequiredService<IAuditLog>()
                                            , Services.GetRequiredService<ILogger<VerifyCommand>>()
                                            , _httpClient);

        var result = await Runner.RunAsync($"Verifying {environment} environment..."
                                         , () => verifyCommand.ExecuteAsync()
                                         , Color.Red);

        Runner.DisplayResult(result);
        Runner.PauseForUser();
    }
}
