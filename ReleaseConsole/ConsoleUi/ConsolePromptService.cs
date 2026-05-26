using CP.Client.Core.Avails;
using ReleaseConsole.Core;
using ReleaseConsole.Services.Interfaces;
using Spectre.Console;
using static ReleaseConsole.Menu.MenuText;
using static ReleaseConsole.Menu.MenuText.Commands;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.ConsoleUi;

public sealed class ConsolePromptService : IConsolePromptService
{
    private readonly IArtifactStorage _artifactStorage;

    private sealed record PromptOption<T>(string Label, T Value, bool IsBack = false);

    public ConsolePromptService(IArtifactStorage artifactStorage)
    {
        _artifactStorage = artifactStorage;
    }

    private PromptResult<T> PromptOrBack<T>( string                       title
                                           , IEnumerable<PromptOption<T>> options )
    {
        var backOption = new PromptOption<T>(Navigation.Back, default!, true);
        var allOptions = options.Append(backOption).ToList();
        var prompt = new SelectionPrompt<PromptOption<T>>().Title(title)
                                                           .UseConverter(option => option.Label)
                                                           .AddChoices(allOptions);
        var selected = AnsiConsole.Prompt(prompt);

        return selected.IsBack
                       ? PromptResult<T>.Back()
                       : PromptResult<T>.Selected(selected.Value);
    }

    public PromptResult<Component> SelectComponent(bool isDeploy = false)
    {
        var options = new List<PromptOption<Component>>
                      {
                              new(ComponentMenu.Api, Component.CpApi)
                            , new(ComponentMenu.LocalAiAssistant, Component.LaaMauiApp)
                      };

        if (isDeploy.Not())
        {
            options.AddRange(new[]
                             {
                                     new PromptOption<Component>("📦  All NuGets"
                                                               , Component.AllNugetComponents)
                             });
        }

        return PromptOrBack(Prompts.SelectComponent, options);
    }

    public PromptResult<Environment> SelectEnvironment(bool allowProd = true)
    {
        var options = new List<PromptOption<Environment>>
                      {
                              new(Environments.Dev, Environment.Dev)
                            , new(Environments.Qa,  Environment.Qa)
                      };

        if (allowProd)
            options.Add(new PromptOption<Environment>(Environments.Prod, Environment.Prod));

        return PromptOrBack(Prompts.SelectEnvironment, options);
    }

    public async Task<PromptResult<Artifact>> SelectArtifactAsync( Component   component
                                                                  , Environment environment
                                                                  , int         numberOfArtifacts = 5 )
    {
        var artifacts          = await _artifactStorage.GetMostRecentArtifactsAsync(component, environment, numberOfArtifacts);
        var availableArtifacts = artifacts.ToList();
        var options            = availableArtifacts.Select(artifact => new PromptOption<Artifact>($"{artifact.Version} - {artifact.Metadata.BuiltFor}"
                                                                                                , artifact))
                                                   .ToList();

        return PromptOrBack(Prompts.SelectVersion, options);
    }

    public PromptResult<string> SelectDbAction()
    {
        var options = new List<PromptOption<string>>
                      {
                              new(DbActions.SwapDb,    DbActions.SwapDb)
                            , new(DbActions.RestoreDb, DbActions.RestoreDb)
                      };

        return PromptOrBack(Prompts.SelectDbAction, options);
    }
}
