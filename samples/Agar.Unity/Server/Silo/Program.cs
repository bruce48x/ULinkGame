using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using Orleans.Hosting;
using ULinkGame.Sample.Silo.Persistence;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(configuration =>
    {
        configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
    })
    .UseOrleans((context, silo) =>
    {
        var configuration = context.Configuration;
        ConfigureOrleansSilo(configuration, silo);

        var connectionString = configuration["Orleans:ConnectionString"]
            ?? throw new InvalidOperationException("Missing configuration: Orleans:ConnectionString");

        silo.AddDapperGrainStorage(AgarSiloStorageNames.GrainStateProvider, options =>
        {
            options.ConnectionString = connectionString;
        });
    })
    .Build();

await host.RunAsync();

static void ConfigureOrleansSilo(IConfiguration configuration, ISiloBuilder silo)
{
    var clusterId = configuration["Orleans:ClusterId"] ?? "dev";
    var serviceId = configuration["Orleans:ServiceId"] ?? "ULinkGame-Server";
    var connectionString = configuration["Orleans:ConnectionString"];
    var invariant = configuration["Orleans:Invariant"] ?? "Npgsql";
    var siloPort = ParsePort(configuration["Orleans:SiloPort"], 11111);
    var gatewayPort = ParsePort(configuration["Orleans:GatewayPort"], 30000);
    var advertisedIPAddress = ParseIPAddress(configuration["Orleans:AdvertisedIPAddress"]);

    silo.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = clusterId;
        options.ServiceId = serviceId;
    });

    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        silo.UseAdoNetClustering(options =>
        {
            options.Invariant = invariant;
            options.ConnectionString = connectionString;
        });
    }
    else
    {
        silo.UseLocalhostClustering(siloPort, gatewayPort, serviceId: serviceId, clusterId: clusterId);
    }

    if (advertisedIPAddress is not null)
    {
        silo.ConfigureEndpoints(advertisedIP: advertisedIPAddress, siloPort: siloPort, gatewayPort: gatewayPort);
    }
}

static int ParsePort(string? rawValue, int fallback)
{
    return int.TryParse(rawValue, out var port) && port > 0
        ? port
        : fallback;
}

static IPAddress? ParseIPAddress(string? rawValue)
{
    if (string.IsNullOrWhiteSpace(rawValue))
    {
        return null;
    }

    return IPAddress.TryParse(rawValue, out var address)
        ? address
        : throw new InvalidOperationException($"Invalid configuration: Orleans:AdvertisedIPAddress '{rawValue}'");
}
