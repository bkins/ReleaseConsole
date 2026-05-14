using System.Diagnostics;
using System.Text;
using CP.Client.Core.Avails;
using ReleaseConsole.Core;
using ReleaseConsole.Services.Interfaces;

namespace ReleaseConsole.Services;

public sealed class PowerShellExecutor : IPowerShellExecutor
{
    public ScriptOutputMode OutputMode { get; set; }

    public PowerShellExecutor(ScriptOutputMode outputMode = ScriptOutputMode.Normal)
    {
        OutputMode = outputMode;
    }

    public async Task<PowerShellResult> ExecuteScriptAsync( string                      scriptPath
                                                          , ComponentType?              component
                                                          , Dictionary<string, string>? parameters = null
                                                          , CancellationToken           ct         = default )
    {
        var arguments = string.Empty;

        switch (component)
        {
            case ComponentType.Laa:
            case ComponentType.Api:
                arguments = BuildArguments(scriptPath
                                         , parameters);
                break;
            
            case ComponentType.CpClientCore
              or ComponentType.CpSharedPrimitives
              or ComponentType.AllNuget:
            {
                var args = new StringBuilder($"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\"");

                if (parameters == null)
                {
                    throw new InvalidOperationException($"Script contract violation: a parameter missing for {scriptPath}");
                }

                arguments = args.ToString();
                break;
            }
            case null:
                arguments = new StringBuilder($"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\"").ToString();
                
                break;
        }

        var startInfo = new ProcessStartInfo
                        {
                                FileName               = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
                              , Arguments              = arguments
                              , RedirectStandardOutput = true
                              , RedirectStandardError  = true
                              , UseShellExecute        = false
                              , CreateNoWindow         = true
                              , StandardOutputEncoding = Encoding.UTF8
                              , StandardErrorEncoding  = Encoding.UTF8
                        };

        var output = new StringBuilder(startInfo.Arguments);
        var error  = new StringBuilder();
        
        using var process = new Process { StartInfo = startInfo };

        process.OutputDataReceived += ( _
                                      , eventArgs ) =>
        {
            if (eventArgs.Data == null) return;
            output.AppendLine(eventArgs.Data);

            // Only show verbose output in Verbose mode
            if (OutputMode != ScriptOutputMode.Verbose) return;

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"  [PS] {eventArgs.Data}");
            Console.ResetColor();
        };

        process.ErrorDataReceived += ( _, eventArgs ) =>
        {
            if (eventArgs.Data == null) return;
            error.AppendLine(eventArgs.Data);

            // Show errors unless in Silent mode
            if (OutputMode == ScriptOutputMode.Silent) return;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  [PS ERROR] {eventArgs.Data}");
            Console.ResetColor();
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        // Ensure output buffers flush
        process.WaitForExit();

        return new PowerShellResult(process.ExitCode == 0
                                  , output.ToString()
                                  , error.ToString()
                                  , process.ExitCode);
    }

    private static string BuildArguments( string                      scriptPath
                                        , Dictionary<string, string>? parameters )
    {
        var args = new StringBuilder($"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\"");

        if (parameters == null
         || parameters.ContainsKey("Environment").Not())
        {
            throw new InvalidOperationException($"Script contract violation: Environment parameter missing for {scriptPath}");
        }

        foreach (var (key, value) in parameters)
        {
            args.Append($" -{key} \"{value}\"");
        }

        return args.ToString();
    }
}