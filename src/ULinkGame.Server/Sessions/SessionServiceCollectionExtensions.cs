using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ULinkGame.Server.Sessions;

public static class SessionServiceCollectionExtensions
{
    public static IServiceCollection AddULinkGameServerSessions(this IServiceCollection services)
    {
        services.TryAddSingleton<IGameSessionDirectory, InMemoryGameSessionDirectory>();
        services.TryAddSingleton<IGameSessionResumeService, GameSessionResumeService>();
        return services;
    }

    public static IServiceCollection AddULinkGameServerSessionCleanup(
        this IServiceCollection services,
        Action<SessionCleanupOptions>? configure = null)
    {
        services.AddULinkGameServerSessions();

        var options = new SessionCleanupOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.AddHostedService<GameSessionCleanupHostedService>();
        return services;
    }
}
