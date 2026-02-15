using Spectre.Console;

namespace ReleaseConsole.Core.Spinners;

public class PulseSpinner : Spinner
{
    private static string? _emoji = "";
    private readonly string[] _frames;
    // = new[]
    //                                     {
    //                                             $"\u001b[38;5;255m{_emoji}\u001b[0m", // grey100 (White)
    //                                             $"\u001b[38;5;252m{_emoji}\u001b[0m", // grey82
    //                                             $"\u001b[38;5;247m{_emoji}\u001b[0m", // grey63
    //                                             $"\u001b[38;5;242m{_emoji}\u001b[0m", // grey42
    //                                             $"\u001b[38;5;237m{_emoji}\u001b[0m", // grey23 (Darkest)
    //                                             $"\u001b[38;5;242m{_emoji}\u001b[0m",
    //                                             $"\u001b[38;5;247m{_emoji}\u001b[0m",
    //                                             $"\u001b[38;5;252m{_emoji}\u001b[0m"
    //                                     };

    public override TimeSpan              Interval  => TimeSpan.FromMilliseconds(80);
    public override bool                  IsUnicode => true;
    public override IReadOnlyList<string> Frames    => _frames;

    public PulseSpinner(string emoji)
    {
        _frames = new[]
                  {
                          $"\u001b[38;5;255m{emoji}\u001b[0m", // Bright
                          $"\u001b[38;5;247m{emoji}\u001b[0m", 
                          $"\u001b[38;5;240m{emoji}\u001b[0m", // Darkest
                          $"\u001b[38;5;247m{emoji}\u001b[0m"
                  };
    }
}
