using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ULinkGame.Server.Actors;

public static class ActorServiceCollectionExtensions
{
    public static IServiceCollection AddULinkGameServerActors(
        this IServiceCollection services,
        Action<ActorRuntimeOptions>? configure = null)
    {
        var options = new ActorRuntimeOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IActorRuntime, InMemoryActorRuntime>();
        return services;
    }
}
