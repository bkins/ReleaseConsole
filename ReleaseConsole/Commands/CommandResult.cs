namespace ReleaseConsole.Services;

public class CommandResult( bool    Success
                                  , string  Message
                                  , string? ErrorDetails = null
)
{
    public static CommandResult Ok( string message ) => new(true
                                                          , message);

    public static CommandResult Fail( string  message
                                    , string? details = null ) => new(false
                                                                    , message
                                                                    , details);

    public bool    Success      { get; init; } = Success;
    public string  Message      { get; set; } = Message;
    public string? ErrorDetails { get; init; } = ErrorDetails;

    public void Deconstruct( out bool    Success
                          , out  string  Message
                          , out  string? ErrorDetails )
    {
        Success      = this.Success;
        Message      = this.Message;
        ErrorDetails = this.ErrorDetails;
    }
}