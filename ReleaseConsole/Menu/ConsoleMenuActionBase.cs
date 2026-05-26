using ReleaseConsole.ConsoleUi;
using ReleaseConsole.Core;

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

    public abstract string Label { get; }
    public abstract string Path  { get; }
    public virtual  int    Order => 0;

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
}
