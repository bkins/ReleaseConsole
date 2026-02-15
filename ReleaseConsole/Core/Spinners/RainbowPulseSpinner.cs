using Spectre.Console;

namespace ReleaseConsole.Core.Spinners;

public class RainbowPulseSpinner : Spinner
{
    private readonly string[] _frames;

    // Faster interval for a smooth "shimmer" effect
    public override TimeSpan              Interval  => TimeSpan.FromMilliseconds(60);
    public override bool                  IsUnicode => true;
    public override IReadOnlyList<string> Frames    => _frames;

    public RainbowPulseSpinner(string symbol = "█")
    {
        // ANSI 256-color IDs for a rainbow: Red, Orange, Yellow, Green, Blue, Purple
        int[] rainbowColors = { 196, 208, 226, 46, 21, 93 };
        
        _frames = rainbowColors
                  .Select(color => $"\u001b[38;5;{color}m{symbol}\u001b[0m")
                  .ToArray();
    }
}