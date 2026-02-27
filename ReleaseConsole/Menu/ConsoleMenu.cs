using CP.Client.Core.Avails;
using Spectre.Console;

namespace ReleaseConsole.Menu;

public sealed class ConsoleMenu
{
    private readonly List<MenuItem> _items = new();

    public string  Title { get; init; } = "";
    public string? Hint  { get; init; }

    public ConsoleMenu Add(string label, Func<Task> action)
    {
        _items.Add(new MenuItem(label, action, IsExit: false));
        return this;
    }

    public ConsoleMenu AddExit(string label)
    {
        _items.Add(new MenuItem(label, () => Task.CompletedTask, IsExit: true));
        return this;
    }

    public async Task<MenuResult> ShowAsync()
    {
        var prompt = new SelectionPrompt<MenuItem>().Title(Title)
                                                    .UseConverter(i => i.Label)
                                                    .AddChoices(_items);
        if (Hint?.HasValue() ?? false )
            prompt.MoreChoicesText(Hint);

        var selected = AnsiConsole.Prompt(prompt);

        if (!selected.IsExit)
            await selected.Action();

        return selected.IsExit
                       ? MenuResult.Exit
                       : MenuResult.Continue;
    }

    private sealed record MenuItem( string     Label
                                  , Func<Task> Action
                                  , bool       IsExit );
}

public enum MenuResult
{
    Continue,
    Exit
}