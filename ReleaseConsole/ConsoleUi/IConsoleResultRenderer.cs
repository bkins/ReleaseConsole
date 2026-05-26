using ReleaseConsole.Services;

namespace ReleaseConsole.ConsoleUi;

public interface IConsoleResultRenderer
{
    void Display(CommandResult result);
}
