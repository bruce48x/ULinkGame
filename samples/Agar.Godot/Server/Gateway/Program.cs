using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Gateway.Features;
using ULinkGame.Server.Features;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddULinkGame(builder.Configuration, features =>
{
    features.Feature<GatewayCoreFeature>("gateway-core");
    features
        .Feature<GatewayBusinessFeature>("gateway-business")
        .After("gateway-core")
        .RequiresFeature("gateway-core")
        .RequiresTransport("websocket")
        .RequiresTransport("kcp");
});

var host = builder.Build();
await host.RunAsync();
