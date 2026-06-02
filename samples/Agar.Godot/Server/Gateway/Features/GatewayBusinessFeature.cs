using Agar.Godot.Sample.State;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Gateway.Hosting;
using Gateway.Realtime;
using Gateway.Services;
using ULinkGame.Server.Features;
using ULinkGame.Server.Hosting;

namespace Gateway.Features;

public sealed class GatewayBusinessFeature : IFeature
{
    public void Configure(IServiceCollection services, IConfiguration config)
    {
        services.AddAgarGodotSampleState();
        services.AddSingleton<SessionDirectory>();
        services.AddSingleton(_ => new ControlPlaneRpcServerOptions(
            GatewayRpcServerOptions.FromConfiguration(
                config,
                "ControlPlane",
                new GatewayRpcServerOptions { Transport = "websocket", Port = 20000, Path = "/ws" })));
        services.AddSingleton(_ => new RealtimeRpcServerOptions(
            GatewayRpcServerOptions.FromConfiguration(
                config,
                "Realtime",
                new GatewayRpcServerOptions { Transport = "kcp", Port = 20001, Path = "" })));
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
