namespace ReleaseConsole.Menu;

public sealed record MenuOption(string Label, Func<Task> Action);

