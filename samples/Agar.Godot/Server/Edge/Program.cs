using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Edge.Hosting;
using Edge.Realtime;
using Edge.Services;
using ULinkGame.Server.Hosting;
using ULinkGame.Server.ReliablePush;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.AddULinkGameServerOrleansClient();

builder.Services.AddSingleton<SessionDirectory>();
builder.Services.AddSingleton(_ => new ControlPlaneRpcServerOptions(
    EdgeRpcServerOptions.FromConfiguration(
        builder.Configuration,
        "ControlPlane",
        new EdgeRpcServerOptions { Transport = "websocket", Port = 20000, Path = "/ws" })));
builder.Services.AddSingleton(_ => new RealtimeRpcServerOptions(
    EdgeRpcServerOptions.FromConfiguration(
        builder.Configuration,
        "Realtime",
        new EdgeRpcServerOptions { Transport = "kcp", Port = 20001, Path = "" })));
builder.Services.AddSingleton<EdgeNodeIdentity>();
builder.Services.AddSingleton<MatchmakingMonitor>();
builder.Services.AddSingleton<RoomRuntimeHost>();
builder.Services.AddSingleton<ReliableMatchmakingPublisher>();
builder.Services.AddULinkGameServerReliablePush();
builder.Services.AddSingleton<EdgeMatchmakingCoordinator>();
builder.Services.AddULinkRpcServer<DefaultControlPlaneRpcServerConfigurator>();
builder.Services.AddULinkRpcServer<DefaultRealtimeRpcServerConfigurator>();
builder.Services.AddHostedService<MatchmakingHostedService>();
builder.Services.AddHostedService<DisconnectedSessionCleanupHostedService>();
builder.Services.AddULinkGameServerGateway();

var host = builder.Build();
await host.RunAsync();
