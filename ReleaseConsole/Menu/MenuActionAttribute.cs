namespace ReleaseConsole.Menu;

/// <summary>
/// Decorates an <see cref="IMenuAction"/> implementation with the metadata needed
/// to place it in the menu without requiring explicit property overrides.
/// </summary>
/// <example>
/// <code>
/// [MenuAction(path: "db/swap", label: "🔄 DB Actions...", Order = 70)]
/// public sealed class DbActionsMenuAction : ConsoleMenuActionBase { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MenuActionAttribute : Attribute
{
    public string Path  { get; }
    public string Label { get; }
    public int    Order { get; init; }

    public MenuActionAttribute(string path, string label)
    {
        Path  = path;
        Label = label;
    }
}
