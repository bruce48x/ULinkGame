using Microsoft.Extensions.DependencyInjection;
using Shared.Gameplay;
using ULinkGame.Server;
using ULinkGame.Server.Actors;
using ULinkGame.Server.Diagnostics;
using ULinkGame.Server.Features;
using ULinkGame.Server.Guardrails;
using ULinkGame.Server.Hosting;
using ULinkGame.Server.Hotfix;
using ULinkGame.Server.Hotfix.Loading;
using ULinkGame.Server.Sessions;

namespace Gateway.Features;

public sealed class GatewayCoreFeature : ULinkGameFeature
{
    public override void ConfigureServices(ULinkGameFeatureContext context)
    {
        var services = context.Services;

        services.AddULinkGameServerActors(options =>
        {
            options.MailboxCapacity = 4096;
            options.CallTimeout = TimeSpan.FromSeconds(5);
            options.SlowMessageThreshold = TimeSpan.FromSeconds(1);
        });
        services.AddULinkGameServer();
        services.AddULinkGameServerSessionCleanup(options =>
        {
            options.Interval = TimeSpan.FromSeconds(30);
            options.DisconnectedEndpointRetention = TimeSpan.FromMinutes(2);
        });
        services.AddMessageRecording();
        services.AddULinkGameRuntimeValidation();

        var hotfixDirectory = Path.Combine(AppContext.BaseDirectory, "hotfix");
        services.AddULinkGameHotfix(
            new CurrentDirectoryHotfixAssemblySource(hotfixDirectory, "Agar.Sample.Hotfix.dll"),
            sharedAssemblyNames: [typeof(ArenaSimulation).Assembly.GetName().Name!]);

        services.AddULinkGameServerGateway();
    }
}
