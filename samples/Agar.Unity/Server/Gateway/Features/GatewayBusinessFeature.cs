using Agar.Sample.State;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Gateway.Hosting;
using Gateway.Realtime;
using Gateway.Services;
using ULinkGame.Server.Configuration;
using ULinkGame.Server.Features;
using ULinkGame.Server.Hosting;

namespace Gateway.Features;

public sealed class GatewayBusinessFeature : ULinkGameFeature
{
    public override void ConfigureServices(ULinkGameFeatureContext context)
    {
        ConfigureServices(context.Services, context.Configuration);
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAgarSampleState();
        services.AddSingleton<SessionDirectory>();

        var runtimeOptions = ULinkGameRuntimeOptions.FromConfiguration(configuration);
        var kcpOptions = runtimeOptions.ToServerRpcServerOptions("kcp");
        services.AddSingleton(kcpOptions);
        services.AddSingleton<IULinkRpcServerConfigurator>(_ =>
            new DefaultControlPlaneRpcServerConfigurator(
                runtimeOptions.ToServerRpcServerOptions("websocket")));
        services.AddSingleton<IULinkRpcServerConfigurator>(_ =>
            new DefaultRealtimeRpcServerConfigurator(kcpOptions));

        services.AddSingleton<GatewayNodeIdentity>();
        services.AddSingleton<MatchmakingMonitor>();
        services.AddSingleton<RoomRuntimeHost>();
        services.AddSingleton<ReliableMatchmakingPublisher>();
        services.AddSingleton<GatewayMatchmakingCoordinator>();
        services.AddHostedService<MatchmakingHostedService>();
        services.AddHostedService<DisconnectedSessionCleanupHostedService>();
    }
}
