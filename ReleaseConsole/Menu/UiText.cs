using Spectre.Console;

namespace ReleaseConsole.Menu;

public sealed class UiText
{
    public        string                 Label          { get; }
    public        string                 PanelTitle     { get; }
    public        string?                StatusTemplate { get; }
    public        Color Color          { get; }

    public UiText(string label, string panelTitle, Color color, string? statusTemplate = null)
    {
        Label          = label;
        PanelTitle     = panelTitle;
        StatusTemplate = statusTemplate;
        Color          = color;
    }

    public string Status(params object[] args)
        => StatusTemplate is null ? string.Empty : string.Format(StatusTemplate, args);
}
