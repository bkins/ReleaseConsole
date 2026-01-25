namespace ReleaseConsole.Services;

public sealed record CommandResult(
    bool    Success,
    string  Message,
    string? ErrorDetails = null
)
{
    public static CommandResult Ok(string   message)                         => new(true, message);
    public static CommandResult Fail(string message, string? details = null) => new(false, message, details);
}