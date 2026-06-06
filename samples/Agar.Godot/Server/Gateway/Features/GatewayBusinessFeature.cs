using Agar.Godot.Sample.State;
using Microsoft.Extensions.DependencyInjection;
using Gateway.Hosting;
using Gateway.Realtime;
using Gateway.Services;
using ULinkGame.Server.Features;
using ULinkGame.Server.Hosting;

namespace Gateway.Features;

public sealed class GatewayBusinessFeature : ULinkGameFeature
{
    public override void ConfigureServices(ULinkGameFeatureContext context)
    {
        ConfigureServices(context.Services, context.Endpoints);
    }

    private static void ConfigureServices(IServiceCollection services, ULinkGameEndpointCatalog endpoints)
    {
        services.AddAgarGodotSampleState();
        services.AddSingleton<SessionDirectory>();
        services.AddSingleton(_ => new ControlPlaneRpcServerOptions(
            GatewayRpcServerOptions.FromEndpoint(endpoints.RequireTransport("websocket"))));
        services.AddSingleton(_ => new RealtimeRpcServerOptions(
            GatewayRpcServerOptions.FromEndpoint(endpoints.RequireTransport("kcp"))));
        services.AddSingleton<GatewayNodeIdentity>();
        services.AddSingleton<MatchmakingMonitor>();
        services.AddSingleton<RoomRuntimeHost>();
        services.AddSingleton<ReliableMatchmakingPublisher>();
        services.AddSingleton<GatewayMatchmakingCoordinator>();
        services.AddULinkRpcServer<DefaultControlPlaneRpcServerConfigurator>();
        services.AddULinkRpcServer<DefaultRealtimeRpcServerConfigurator>();
        services.AddHostedService<MatchmakingHostedService>();
        services.AddHostedService<DisconnectedSessionCleanupHostedService>();
    }
}
