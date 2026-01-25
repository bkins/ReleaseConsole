namespace ReleaseConsole.Services;

public interface ICommand
{
    string              Name        { get; }
    string              Description { get; }
    Task<CommandResult> ExecuteAsync(CancellationToken ct = default);
}