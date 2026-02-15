using ReleaseConsole.Services;

namespace ReleaseConsole.Commands;

public interface ICommand
{
    string              Name        { get; }
    string              Description { get; }
    Task<CommandResult> ExecuteAsync(Action<string>? report = null);
}