using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ULinkGame.Server.Hosting;
using ULinkGame.Sample.Silo.Persistence;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(configuration =>
    {
        configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
    })
    .UseULinkGameServerOrleansSilo((context, silo) =>
    {
        var configuration = context.Configuration;
        var connectionString = configuration["Orleans:ConnectionString"]
            ?? throw new InvalidOperationException("Missing configuration: Orleans:ConnectionString");

        silo.AddDapperGrainStorage(AgarSiloStorageNames.GrainStateProvider, options =>
        {
            options.ConnectionString = connectionString;
        });
    })
    .Build();

await host.RunAsync();
