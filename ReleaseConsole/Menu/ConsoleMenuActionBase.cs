using ReleaseConsole.ConsoleUi;
using ReleaseConsole.Core;
using System.Reflection;

namespace ReleaseConsole.Menu;

public abstract class ConsoleMenuActionBase : IMenuAction
{
    protected IConsolePromptService     Prompts  { get; }
    protected ICommandExecutionPipeline Runner   { get; }
    protected IServiceProvider          Services { get; }

    protected ConsoleMenuActionBase( IConsolePromptService     prompts
                                   , ICommandExecutionPipeline runner
                                   , IServiceProvider          services )
    {
        Prompts  = prompts;
        Runner   = runner;
        Services = services;
    }

    /// <summary>
    /// Menu item label. Reads from <see cref="MenuActionAttribute"/> by default.
    /// Override only when the label cannot be expressed as a compile-time attribute argument.
    /// Throws <see cref="InvalidOperationException"/> if neither the attribute nor an override
    /// provides a value — so the mistake surfaces at first access, not silently as an empty entry.
    /// </summary>
    public virtual string Label => ResolveAttribute()?.Label
        ?? throw new InvalidOperationException(MissingAttributeMessage(nameof(Label)));

    /// <summary>
    /// Dot/slash-separated path used for future hierarchical menu routing
    /// (e.g. <c>"db/swap"</c>). Reads from <see cref="MenuActionAttribute"/> by default.
    /// Throws <see cref="InvalidOperationException"/> if the attribute is absent and no override
    /// is provided.
    /// </summary>
    public virtual string Path  => ResolveAttribute()?.Path
        ?? throw new InvalidOperationException(MissingAttributeMessage(nameof(Path)));

    /// <summary>
    /// Determines the position of this action in the menu. Lower values appear first.
    /// Defaults to the value declared on <see cref="MenuActionAttribute"/>, or 0 if absent.
    /// </summary>
    public virtual int    Order => ResolveAttribute()?.Order ?? 0;

    public abstract Task ExecuteAsync();

    protected static bool TrySelect<T>( PromptResult<T> result
                                      , out T           value )
    {
        if (result.Kind != PromptResultKind.Selected)
        {
            value = default!;
            return false;
        }

        value = result.Value!;
        return true;
    }

    private MenuActionAttribute? ResolveAttribute() =>
        GetType().GetCustomAttribute<MenuActionAttribute>(inherit: false);

    private string MissingAttributeMessage(string propertyName) =>
        $"{GetType().Name} must be decorated with [{nameof(MenuActionAttribute)}] "
      + $"or override {propertyName}.";
}
