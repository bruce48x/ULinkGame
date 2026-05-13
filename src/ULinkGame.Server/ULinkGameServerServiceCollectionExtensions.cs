using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ULinkGame.Server.Actors;
using ULinkGame.Server.ReliablePush;
using ULinkGame.Server.Sessions;

namespace ULinkGame.Server;

public static class ULinkGameServerServiceCollectionExtensions
{
    public static IServiceCollection AddULinkGameServer(this IServiceCollection services)
    {
        services.AddULinkGameServerActors();
        services.AddULinkGameServerSessions();
        services.AddULinkGameServerReliablePush();
        services.TryAddSingleton<IULinkGameServer, ULinkGameServer>();
        return services;
    }
}
