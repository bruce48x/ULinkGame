using Microsoft.Extensions.Configuration;
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

public sealed class GatewayCoreFeature : IFeature
{
    public void Configure(IServiceCollection services, IConfiguration config)
    {
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

        var hotfixDirectory = ResolveHotfixDirectory(
            config["Hotfix:Directory"] ?? "../../../../Hotfix/bin/Debug/net10.0");
        var hotfixAssembly = config["Hotfix:Assembly"] ?? "Agar.Sample.Hotfix.dll";
        services.AddULinkGameHotfix(
            new CurrentDirectoryHotfixAssemblySource(hotfixDirectory, hotfixAssembly),
            sharedAssemblyNames: [typeof(ArenaSimulation).Assembly.GetName().Name!]);

        services.AddULinkGameServerGateway();
    }

    private static string ResolveHotfixDirectory(string configuredDirectory)
    {
        if (Path.IsPathFullyQualified(configuredDirectory))
        {
            return configuredDirectory;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredDirectory));
    }
}
