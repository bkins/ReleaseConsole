using Spectre.Console;

namespace ReleaseConsole.Menu;

public static class MenuText
{
    public const string MainMenuTitle = "[cyan]What would you like to do?[/]";

    public static class Header
    {
        public const string Title    = "Release Console";
        public const string Subtitle = "[dim]Build. Deploy. Verify. Report.[/]";
    }

    public static class Navigation
    {
        public const string Back            = "⬅️ Back";
        public const string Exit            = "❌  Exit";
        public const string PressAnyKey     = "[grey]Press any key to continue...[/]";
        public const string MoreChoicesHint = "[grey](Move up and down to reveal more options)[/]";
        public const string Goodby          = "\n[cyan]Goodbye![/]";
    }

    public static class Prompts
    {
        public const string SelectComponent   = "[cyan]Select component:[/]";
        public const string SelectEnvironment = "[cyan]Select environment:[/]";
        public const string SelectVersion     = "[cyan]Select version:[/]";
        public const string SelectDbAction    = "[cyan]Select DB action:[/]";
        public const string EnterScriptPath   = "[cyan]Enter script path:[/]";
    }

    public static class Commands
    {
        public static readonly UiText BuildComponent = new(label: "🔨 Build Component"
                                                         , panelTitle: "[cyan]BUILD COMPONENT[/]"
                                                         , Color.Cyan1
                                                         , statusTemplate: "Building {0}");

        public static readonly UiText DeployComponent = new(label: "🚀 Deploy Component"
                                                          , panelTitle: "[yellow]DEPLOY COMPONENT[/]"
                                                          , Color.Cyan1
                                                          , statusTemplate: "Deploying {0} to {1}...");

        public static readonly UiText VerifyEnvironment = new(label: "✅  Verify Environment"
                                                            , panelTitle: "[green]VERIFY ENVIRONMENT[/]"
                                                            , Color.Cyan1
                                                            , statusTemplate: "Verifying {0} environment...");

        public static readonly UiText RollbackProduction = new(label: "↩️ Rollback Production"
                                                             , panelTitle: "[red]ROLLBACK PRODUCTION[/]"
                                                             , Color.Cyan1
                                                             , statusTemplate: "Rolling back {0}...");

        public static readonly UiText ViewAuditLogs = new(label: "📋 View Audit Logs"
                                                        , panelTitle: "[cyan]RECENT AUDIT LOGS[/]"
                                                        , Color.Cyan1);

        public static readonly UiText ListArtifacts = new(label: "📦 List Artifacts"
                                                        , panelTitle: "[cyan]LIST ARTIFACTS[/]"
                                                        , Color.Cyan1);

        public static readonly UiText LaunchScalar = new(label: "🧭 Start API..."
                                                       , panelTitle: "[cyan]Scalar[/]"
                                                       , Color.Cyan1);
        //☁️🔢🔄
        public static readonly UiText DbAction = new(label: "🔄 DB Actions..."
                                                       , panelTitle: "[cyan]DB Swap & Retore[/]"
                                                       , Color.Cyan1);
    }

    public static class ComponentMenu
    {
        public const string Api              = "🧠  API (CpApi)";
        public const string LocalAiAssistant = "📱  LocalAiAssistant (Laa) -> All Envs";
        public const string SharedPrimitives = "🛠️ CpSharedPrimitives";
        public const string ClientCore       = "⚗️  CpClientCore";
        public const string AllNugets        = "📦  All NuGets";
    }

    public static class Environments
    {
        public const string Dev  = "🔨 Dev";
        public const string Qa   = "🧪 QA";
        public const string Prod = "🚀 Prod";
    }
    
    public static class Results
    {
        public const string Success = "✅  SUCCESS ";
        public const string Failure = "⛔  FAILURE";
    }

    public static class DbActions
    {//️⬆️️️️🔁 '⬆️️️️', '️️️️️️️️️️️️️️️️️️️️⬆️️️️', '⬆'
        public const string SwapDb = "⬆️ Prod to Dev";
        public const string RestoreDb = "🔁 Restore Dev";
        
        /*
         * Visible	Internal
⬆	base symbol only
⬆️	base symbol + emoji style
⬆️️️️	base symbol + redundant invisible modifiers
         */
    }
}
