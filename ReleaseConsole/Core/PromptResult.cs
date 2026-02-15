namespace ReleaseConsole.Core;

public readonly struct PromptResult<T>
{
    public PromptResultKind Kind   { get; init;  }
    public bool             IsBack { get; }
    public T?               Value  { get; }

    private PromptResult(bool isBack, T? value)
    {
        IsBack = isBack;
        Value  = value;
    }

    public PromptResult( PromptResultKind kind
                       , T?               value )
    {
        Kind  = kind;
        Value = value;
    }

    public PromptResult( PromptResultKind kind )
    {
        Kind = kind;
    }

    public static PromptResult<T> Back()            => new(true, default);
    public static PromptResult<T> Selected(T value) => new(false, value);
}