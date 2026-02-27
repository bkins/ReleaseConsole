namespace ReleaseConsole.Core;

public sealed record PromptResult<T>
{
    public PromptResultKind Kind  { get; }
    public T?               Value { get; }

    private PromptResult(PromptResultKind kind, T? value)
    {
        Kind  = kind;
        Value = value;
    }

    public bool IsBack     => Kind == PromptResultKind.Back;
    public bool IsSelected => Kind == PromptResultKind.Selected;

    public static PromptResult<T> Back() 
        => new(PromptResultKind.Back, default);

    public static PromptResult<T> Selected(T value) 
        => new(PromptResultKind.Selected, value);
}
