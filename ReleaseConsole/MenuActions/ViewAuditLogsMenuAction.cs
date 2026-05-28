using CP.Client.Core.Avails;
using Microsoft.Extensions.DependencyInjection;
using ReleaseConsole.ConsoleUi;
using ReleaseConsole.Menu;
using ReleaseConsole.Services.Interfaces;
using Spectre.Console;
using static ReleaseConsole.Menu.MenuText.Commands;

namespace ReleaseConsole.MenuActions;

[MenuAction(path: "audit", label: "📋 View Audit Logs", Order = 40)]
public sealed class ViewAuditLogsMenuAction : ConsoleMenuActionBase
{
    public ViewAuditLogsMenuAction( IConsolePromptService     prompts
                                  , ICommandExecutionPipeline runner
                                  , IServiceProvider          services )
        : base(prompts, runner, services) { }

    public override async Task ExecuteAsync()
    {
        Runner.WritePanel(ViewAuditLogs.PanelTitle, Color.Cyan1);

        var auditLog = Services.GetRequiredService<IAuditLog>();
        var entries  = await auditLog.GetRecentEntriesAsync(20);

        if (entries.Any().Not())
        {
            AnsiConsole.MarkupLine("[yellow]No audit entries found.[/]");
            Runner.PauseForUser();
            return;
        }

        var table = new Table().Border(TableBorder.Rounded)
                               .BorderColor(Color.Grey)
                               .AddColumn(new TableColumn("Status").Centered())
                               .AddColumn("Timestamp")
                               .AddColumn("Command")
                               .AddColumn("Component")
                               .AddColumn("Env")
                               .AddColumn("User");

        foreach (var entry in entries)
        {
            var statusMarkup = entry.Success
                                       ? "[green]  ✅   [/]"
                                       : "[red]  ⛔   [/]";
            var envMarkup = entry.Environment?.ToString() ?? "N/A";

            table.AddRow(statusMarkup
                       , entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                       , entry.Command
                       , entry.Component
                       , envMarkup
                       , entry.User);
        }

        AnsiConsole.Write(table);
        Runner.PauseForUser();
    }
}
