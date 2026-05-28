using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ULinkGame.Server.Hotfix.Loading;

namespace ULinkGame.Server.Hotfix;

public static class HotfixServiceCollectionExtensions
{
    public static IServiceCollection AddULinkGameHotfix(
        this IServiceCollection services,
        IHotfixAssemblySource source)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(source);
        services.TryAddSingleton(source);
        services.TryAddSingleton<IHotfixManager, HotfixManager>();
        return services;
    }
}
