using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ReleaseConsole.Menu;

public static class MenuActionRegistrationExtensions
{
    public static IServiceCollection AddMenuActions(this IServiceCollection services)
    {
        var actionTypes = Assembly.GetExecutingAssembly()
                                  .GetTypes()
                                  .Where(type => !type.IsAbstract && typeof(IMenuAction).IsAssignableFrom(type));

        foreach (var actionType in actionTypes)
            services.AddTransient(typeof(IMenuAction), actionType);

        return services;
    }
}
