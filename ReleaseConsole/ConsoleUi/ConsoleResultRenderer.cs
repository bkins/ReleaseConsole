using CP.Client.Core.Avails;
using ReleaseConsole.Services;
using Spectre.Console;
using static ReleaseConsole.Menu.MenuText;

namespace ReleaseConsole.ConsoleUi;

public sealed class ConsoleResultRenderer : IConsoleResultRenderer
{
    public void Display(CommandResult result)
    {
        AnsiConsole.WriteLine();

        if (result.Success)
        {
            var successPanel = new Panel($"[green]{Results.Success} [/]\n\n{result.Message.EscapeMarkup()}").BorderColor(Color.Green);
            AnsiConsole.Write(successPanel);
        }
        else
        {
            var innerPanelText = result.Message.HasValue()
                                         ? $"\n\n{result.Message.EscapeMarkup()}"
                                         : string.Empty;

            var failurePanel = new Panel($"[red]{Results.Failure}[/]{innerPanelText}").BorderColor(Color.Red);
            AnsiConsole.Write(failurePanel);

            if (result.ErrorDetails?.HasValue() ?? false)
            {
                AnsiConsole.MarkupLine("\n[yellow]Details:[/]");
                AnsiConsole.MarkupLine($"[grey]{result.ErrorDetails.EscapeMarkup()}[/]");
            }
        }

        AnsiConsole.WriteLine();
    }
}
