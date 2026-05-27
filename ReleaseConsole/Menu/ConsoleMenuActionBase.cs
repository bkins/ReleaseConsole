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
    /// Menu item label. Defaults to the value declared on <see cref="MenuActionAttribute"/>.
    /// Override only when the label cannot be expressed as a compile-time attribute argument.
    /// </summary>
    public virtual string Label => ResolveAttribute()?.Label ?? string.Empty;

    /// <summary>
    /// Dot/slash-separated path used for future hierarchical menu routing
    /// (e.g. <c>"db/swap"</c>). Defaults to the value declared on <see cref="MenuActionAttribute"/>.
    /// </summary>
    public virtual string Path  => ResolveAttribute()?.Path  ?? string.Empty;

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
}
