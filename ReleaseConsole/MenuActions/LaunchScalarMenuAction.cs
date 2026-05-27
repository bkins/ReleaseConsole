using CP.Client.Core.Avails;
using ReleaseConsole.ConsoleUi;
using ReleaseConsole.Core;
using ReleaseConsole.Menu;
using Spectre.Console;
using System.Diagnostics;
using static ReleaseConsole.Menu.MenuText.Commands;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.MenuActions;

public sealed class LaunchScalarMenuAction : ConsoleMenuActionBase
{
    private const string ScriptFolder   = @"C:\CP\Deploy\Api\";
    private const string ScriptFileName = "start-api.bat";

    private readonly HttpClient _httpClient;

    public LaunchScalarMenuAction( IConsolePromptService     prompts
                                 , ICommandExecutionPipeline runner
                                 , IServiceProvider          services
                                 , HttpClient                httpClient )
        : base(prompts, runner, services)
    {
        _httpClient = httpClient;
    }

    public override string Label => LaunchScalar.Label;
    public override string Path  => "scalar";

    public override async Task ExecuteAsync()
    {
        Runner.WritePanel(LaunchScalar.PanelTitle, LaunchScalar.Color);

        if (TrySelect(Prompts.SelectEnvironment(), out var environment).Not())
            return;

        AnsiConsole.MarkupLine($"[green]Launching Scalar in {environment}...[/]");

        StartApi(environment);
        await WaitForApiReady(environment);

        Runner.PauseForUser();
    }

    private static void StartApi(Environment environment)
    {
        var scriptFolderPath = $@"{ScriptFolder}{environment}";
        var scriptPath       = System.IO.Path.Combine(scriptFolderPath, ScriptFileName);

        if (File.Exists(scriptPath).Not())
        {
            AnsiConsole.MarkupLine($"[red]Script not found: {scriptPath}[/]");
            return;
        }

        var psi = new ProcessStartInfo
                  {
                          FileName         = "wt.exe"
                        , Arguments        = @$"--profile ""{environment.ToString().ToUpper()}: CP API"""
                        , UseShellExecute  = true
                        , CreateNoWindow   = false
                        , WorkingDirectory = scriptFolderPath
                  };

        try
        {
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }

    private async Task WaitForApiReady(Environment environment)
    {
        var healthEndpoint = ApiEnvironments.Url(environment) + "/Scalar";

        while (true)
        {
            try
            {
                var response = await _httpClient.GetAsync(healthEndpoint);
                if (response.IsSuccessStatusCode) return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // API not ready yet
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}
