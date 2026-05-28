using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ULinkGame.Server.Hotfix.Loading;

namespace ULinkGame.Server.Hotfix;

public static class HotfixServiceCollectionExtensions
{
    public static IServiceCollection AddULinkGameHotfix(
        this IServiceCollection services,
        IHotfixAssemblySource source,
        IEnumerable<string>? sharedAssemblyNames = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(source);
        var sharedNames = (sharedAssemblyNames ?? Array.Empty<string>()).ToArray();
        services.RemoveAll<IHotfixAssemblySource>();
        services.AddSingleton(source);
        services.TryAddSingleton<IHotfixManager>(provider =>
            new HotfixManager(provider.GetRequiredService<IHotfixAssemblySource>(), sharedNames));
        return services;
    }
}
