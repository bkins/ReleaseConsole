using Spectre.Console;
using static ReleaseConsole.Menu.MenuText;

namespace ReleaseConsole.ConsoleUi;

public sealed class ConsolePauseService : IConsolePauseService
{
    public void Pause()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Markup(Navigation.PressAnyKey);
        Console.ReadKey(true);
    }
}
