using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using Orleans.Hosting;

namespace ULinkGame.Server.Hosting;

public static class ULinkGameServerExtensions
{
    public static IHostApplicationBuilder AddULinkGameServerOrleansClient(this IHostApplicationBuilder builder)
    {
        builder.UseOrleansClient(client =>
        {
            var configuration = builder.Configuration;
            var clusterId = configuration["Orleans:ClusterId"] ?? "dev";
            var serviceId = configuration["Orleans:ServiceId"] ?? "ULinkGame-Server";

            client.Configure<ClusterOptions>(options =>
            {
                options.ClusterId = clusterId;
                options.ServiceId = serviceId;
            });

            client.UseLocalhostClustering(serviceId: serviceId, clusterId: clusterId);
        });

        return builder;
    }

    public static IHostBuilder UseULinkGameServerOrleansSilo(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseULinkGameServerOrleansSilo(configureSilo: null);
    }

    public static IHostBuilder UseULinkGameServerOrleansSilo(
        this IHostBuilder hostBuilder,
        Action<HostBuilderContext, ISiloBuilder>? configureSilo)
    {
        return hostBuilder.UseOrleans((context, silo) =>
        {
            var configuration = context.Configuration;
            var clusterId = configuration["Orleans:ClusterId"] ?? "dev";
            var serviceId = configuration["Orleans:ServiceId"] ?? "ULinkGame-Server";
            var siloPort = ParsePort(configuration["Orleans:SiloPort"], 11111);
            var gatewayPort = ParsePort(configuration["Orleans:GatewayPort"], 30000);
            var advertisedIPAddress = ParseIPAddress(configuration["Orleans:AdvertisedIPAddress"]);

            silo.Configure<ClusterOptions>(options =>
            {
                options.ClusterId = clusterId;
                options.ServiceId = serviceId;
            });

            silo.UseLocalhostClustering(siloPort, gatewayPort, serviceId: serviceId, clusterId: clusterId);

            if (advertisedIPAddress is not null)
            {
                silo.ConfigureEndpoints(advertisedIP: advertisedIPAddress, siloPort: siloPort, gatewayPort: gatewayPort);
            }

            configureSilo?.Invoke(context, silo);
        });
    }

    private static int ParsePort(string? rawValue, int fallback)
    {
        return int.TryParse(rawValue, out var port) && port > 0
            ? port
            : fallback;
    }

    private static IPAddress? ParseIPAddress(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return IPAddress.TryParse(rawValue, out var address)
            ? address
            : throw new InvalidOperationException($"Invalid configuration: Orleans:AdvertisedIPAddress '{rawValue}'");
    }
}
