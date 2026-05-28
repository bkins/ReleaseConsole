using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ReleaseConsole.Menu;

public static class MenuActionRegistrationExtensions
{
    /// <summary>
    /// Scans the executing assembly for all concrete <see cref="IMenuAction"/> implementations
    /// and registers each as transient. Throws <see cref="InvalidOperationException"/> at
    /// startup if any concrete action is missing a <see cref="MenuActionAttribute"/>, so the
    /// mistake is caught before the menu is ever rendered.
    /// </summary>
    public static IServiceCollection AddMenuActions(this IServiceCollection services)
    {
        var concreteActionTypes = Assembly.GetExecutingAssembly()
                                          .GetTypes()
                                          .Where(type => !type.IsAbstract
                                                      && typeof(IMenuAction).IsAssignableFrom(type))
                                          .ToList();

        EnforceAttributePresence(concreteActionTypes);

        foreach (var actionType in concreteActionTypes)
            services.AddTransient(typeof(IMenuAction), actionType);

        return services;
    }

    private static void EnforceAttributePresence(IReadOnlyList<Type> actionTypes)
    {
        var missing = actionTypes
            .Where(actionType => actionType.GetCustomAttribute<MenuActionAttribute>() is null)
            .Select(actionType => actionType.Name)
            .ToList();

        if (missing.Count == 0)
            return;

        var names = string.Join(", ", missing);
        throw new InvalidOperationException(
            $"The following {nameof(IMenuAction)} types are missing [{nameof(MenuActionAttribute)}]: {names}. "
          + "Decorate each class with [MenuAction(path, label)] to register it.");
    }
}
