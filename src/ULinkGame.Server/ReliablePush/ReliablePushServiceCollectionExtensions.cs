using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ULinkGame.Server.ReliablePush;

public static class ReliablePushServiceCollectionExtensions
{
    public static IServiceCollection AddULinkGameServerReliablePush(
        this IServiceCollection services,
        Action<ReliablePushOptions>? configure = null)
    {
        var options = new ReliablePushOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IReliablePushOutbox, InMemoryReliablePushOutbox>();
        services.TryAddSingleton<IReliablePushAckService, ReliablePushAckService>();
        return services;
    }
}
