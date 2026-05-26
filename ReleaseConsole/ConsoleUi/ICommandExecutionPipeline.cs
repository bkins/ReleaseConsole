using ReleaseConsole.Services;
using Spectre.Console;

namespace ReleaseConsole.ConsoleUi;

public interface ICommandExecutionPipeline
{
    Task<CommandResult> RunAsync(string spinnerText, Func<Task<CommandResult>> action, Color? color = null);
    Task<CommandResult> RunAsync(string spinnerText, Func<Action<string>, Task<CommandResult>> action, Color? color = null, string messageSuffix = "");
    void WritePanel(string title, Color borderColor);
    void DisplayResult(CommandResult result);
    void PauseForUser();
}
