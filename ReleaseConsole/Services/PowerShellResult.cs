namespace ReleaseConsole.Services;

public sealed record PowerShellResult(
    bool   Success,
    string Output,
    string Error,
    int    ExitCode
);