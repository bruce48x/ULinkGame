using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Agar.Sample.State;
using Gateway.Hosting;
using Gateway.Realtime;
using Gateway.Services;
using Shared.Gameplay;
using ULinkGame.Server.Hotfix;
using ULinkGame.Server.Hotfix.Loading;
using ULinkGame.Server.Hosting;
using ULinkGame.Server.ReliablePush;
using ULinkGame.Server.Sessions;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddAgarSampleState();
builder.Services.AddULinkGameServerSessions();
builder.Services.AddSingleton<SessionDirectory>();
builder.Services.AddSingleton(_ => new ControlPlaneRpcServerOptions(
    GatewayRpcServerOptions.FromConfiguration(
        builder.Configuration,
        "ControlPlane",
        new GatewayRpcServerOptions { Transport = "websocket", Port = 20000, Path = "/ws" })));
builder.Services.AddSingleton(_ => new RealtimeRpcServerOptions(
    GatewayRpcServerOptions.FromConfiguration(
        builder.Configuration,
        "Realtime",
        new GatewayRpcServerOptions { Transport = "kcp", Port = 20001, Path = "" })));
builder.Services.AddSingleton<GatewayNodeIdentity>();
builder.Services.AddSingleton<MatchmakingMonitor>();
builder.Services.AddSingleton<RoomRuntimeHost>();
builder.Services.AddSingleton<ReliableMatchmakingPublisher>();
builder.Services.AddULinkGameServerReliablePush();
builder.Services.AddSingleton<GatewayMatchmakingCoordinator>();
builder.Services.AddULinkRpcServer<DefaultControlPlaneRpcServerConfigurator>();
builder.Services.AddULinkRpcServer<DefaultRealtimeRpcServerConfigurator>();
builder.Services.AddHostedService<MatchmakingHostedService>();
builder.Services.AddHostedService<DisconnectedSessionCleanupHostedService>();
var hotfixDirectory = builder.Configuration["Hotfix:Directory"] ?? "../Hotfix/bin/Debug/net10.0";
var hotfixAssembly = builder.Configuration["Hotfix:Assembly"] ?? "Agar.Sample.Hotfix.dll";
builder.Services.AddULinkGameHotfix(
    new CurrentDirectoryHotfixAssemblySource(hotfixDirectory, hotfixAssembly),
    sharedAssemblyNames: [typeof(ArenaSimulation).Assembly.GetName().Name!]);
builder.Services.AddULinkGameServerGateway();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var hotfix = scope.ServiceProvider.GetRequiredService<IHotfixManager>();
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Gateway.Hotfix");
    var result = await hotfix.ReloadAsync();
    if (result.Succeeded)
    {
        logger.LogInformation(
            "Initial hotfix load succeeded from {HotfixPath} with {MethodCount} method(s).",
            result.Current.SourcePath,
            result.Current.Methods.Count);
    }
    else
    {
        logger.LogWarning(
            "Initial hotfix load failed for {HotfixPath}: {ErrorMessage}",
            result.RequestedPath,
            result.ErrorMessage);
        foreach (var diagnostic in result.Diagnostics)
        {
            logger.LogWarning("Hotfix diagnostic: {Diagnostic}", diagnostic);
        }
    }
}

await host.RunAsync();
