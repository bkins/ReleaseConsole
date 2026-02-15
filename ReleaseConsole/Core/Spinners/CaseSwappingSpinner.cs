using System.Diagnostics;
using Spectre.Console;

namespace ReleaseConsole.Core.Spinners;

public class CaseSwappingSpinner: Spinner
{
    private readonly string[] _frames;
 
    public override  TimeSpan              Interval  => TimeSpan.FromMilliseconds(20);
    public override  bool                  IsUnicode => false;
    public override  IReadOnlyList<string> Frames    => _frames;

    public CaseSwappingSpinner(string text)
    {   
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException("Text cannot be empty", nameof(text));

        // Create the forward path
        var forward = new List<string>();
        for (int i = 0; i < text.Length; i++)
        {
            forward.Add(GetSwappedFrame(text, i));
        }

        // Create the backward path (skip first and last to prevent stutter)
        var backward = new List<string>(forward);
        backward.Reverse();
        var bounceEffect = backward.Skip(1).Take(text.Length - 2);

        // Combine: Forward + Backward
        _frames = forward.Concat(bounceEffect).ToArray();

    }

    private string GetSwappedFrame(string text, int index)
    {
        var chars = text.ToCharArray();
        chars[index] = char.IsUpper(chars[index]) 
                               ? char.ToLower(chars[index]) 
                               : char.ToUpper(chars[index]);
        
        // var ts = _stopwatch.Elapsed;
        // var elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}"
        //                               , ts.Hours
        //                               , ts.Minutes
        //                               , ts.Seconds
        //                               , ts.Milliseconds / 10); // Format for hh:mm:ss.ff
        return new string(chars); // + elapsedTime;
    }
}