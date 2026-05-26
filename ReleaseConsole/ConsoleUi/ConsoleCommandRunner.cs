using CP.Client.Core.Avails;
using ReleaseConsole.Core.Spinners;
using ReleaseConsole.Services;
using Spectre.Console;

namespace ReleaseConsole.ConsoleUi;

public sealed class ConsoleCommandRunner : ICommandExecutionPipeline
{
    private readonly IConsoleResultRenderer _renderer;
    private readonly IConsolePauseService   _pause;

    public ConsoleCommandRunner(IConsoleResultRenderer renderer, IConsolePauseService pause)
    {
        _renderer = renderer;
        _pause    = pause;
    }

    public async Task<CommandResult> RunAsync( string                    spinnerText
                                             , Func<Task<CommandResult>> action
                                             , Color?                    color = null )
    {
        return await AnsiConsole.Status()
                                .Spinner(Spinner.Known.Dots)
                                .SpinnerStyle(color ?? Color.White)
                                .StartAsync(spinnerText
                                          , async _ => await action());
    }

    public async Task<CommandResult> RunAsync( string                                   spinnerText
                                             , Func<Action<string>, Task<CommandResult>> action
                                             , Color?                                   color = null
                                             , string                                   messageSuffix = "" )
    {
        var suffix = messageSuffix.HasValue() ? $" - {messageSuffix}" : spinnerText;

        return await AnsiConsole.Status()
                                .Spinner(new CaseSwappingSpinner(spinnerText))
                                .SpinnerStyle(color ?? Color.White)
                                .StartAsync(suffix
                                          , async ctx =>
                                            {
                                                void Report(string text) => ctx.Status(text);
                                                return await action(Report);
                                            });
    }

    public void WritePanel(string title, Color borderColor)
    {
        AnsiConsole.Write(new Panel(title).BorderColor(borderColor));
        AnsiConsole.WriteLine();
    }

    public void DisplayResult(CommandResult result) => _renderer.Display(result);

    public void PauseForUser() => _pause.Pause();
}
