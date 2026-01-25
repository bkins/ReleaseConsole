using System.Diagnostics;
using System.Text;

namespace ReleaseConsole.Services;

public sealed class PowerShellExecutor : IPowerShellExecutor
{
    private readonly bool _streamOutput;

    public PowerShellExecutor(bool streamOutput = true)
    {
        _streamOutput = streamOutput;
    }

    public async Task<PowerShellResult> ExecuteScriptAsync( string                      scriptPath,
                                                            Dictionary<string, string>? parameters = null,
                                                            CancellationToken           ct         = default)
    {
        if (!File.Exists(scriptPath))
        {
            return new PowerShellResult(
                false,
                string.Empty,
                $"Script not found: {scriptPath}",
                -1
            );
        }

        var startInfo = new ProcessStartInfo
                        {
                                FileName               = "pwsh",
                                Arguments              = BuildArguments(scriptPath, parameters),
                                RedirectStandardOutput = true,
                                RedirectStandardError  = true,
                                UseShellExecute        = false,
                                CreateNoWindow         = true
                        };

        var output = new StringBuilder();
        var error  = new StringBuilder();

        using var process = new Process { StartInfo = startInfo };
        
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
                
                if (_streamOutput)
                {
                    // Write to console in real-time with color
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine($"  [PS] {e.Data}");
                    Console.ResetColor();
                }
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                error.AppendLine(e.Data);
                
                if (_streamOutput)
                {
                    // Write errors to console in real-time with warning color
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  [PS ERROR] {e.Data}");
                    Console.ResetColor();
                }
            }
        };

        if (_streamOutput)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Executing: {Path.GetFileName(scriptPath)}");
            Console.ResetColor();
            Console.WriteLine();
        }

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        if (_streamOutput)
        {
            Console.WriteLine();
        }

        return new PowerShellResult(
            process.ExitCode == 0,
            output.ToString(),
            error.ToString(),
            process.ExitCode
        );
    }

    private static string BuildArguments(string scriptPath, Dictionary<string, string>? parameters)
    {
        var args = new StringBuilder($"-ExecutionPolicy Bypass -File \"{scriptPath}\"");

        if (parameters is not null)
        {
            foreach (var (key, value) in parameters)
            {
                args.Append($" -{key} \"{value}\"");
            }
        }

        return args.ToString();
    }
}