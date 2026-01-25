namespace ReleaseConsole.Core;

public sealed record AuditEntry( DateTime     Timestamp,
                                 string       Command,
                                 string       Component,
                                 Environment? Environment,
                                 string       User,
                                 string       Machine,
                                 string       Details,
                                 bool         Success,
                                 string?      ErrorMessage = null );