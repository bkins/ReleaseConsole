namespace ReleaseConsole.Menu;

public interface IMenuAction
{
    string Label { get; }
    string Path  { get; }
    int    Order { get; }
    Task ExecuteAsync();
}
