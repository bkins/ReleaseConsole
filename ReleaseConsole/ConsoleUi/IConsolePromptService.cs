using ReleaseConsole.Core;
using Environment = ReleaseConsole.Core.Environment;

namespace ReleaseConsole.ConsoleUi;

public interface IConsolePromptService
{
    PromptResult<Component> SelectComponent(bool isDeploy = false);
    PromptResult<Environment> SelectEnvironment(bool allowProd = true);
    Task<PromptResult<Artifact>> SelectArtifactAsync(Component component, Environment environment, int numberOfArtifacts = 5);
    PromptResult<string> SelectDbAction();
}
