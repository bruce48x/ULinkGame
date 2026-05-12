using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orleans.Hosting;
using Orleans.Runtime;
using Orleans.Storage;

namespace ULinkGame.Sample.Silo.Persistence;

public static class DapperSiloBuilderExtensions
{
    public static ISiloBuilder AddDapperGrainStorage(
        this ISiloBuilder builder,
        string providerName,
        Action<DapperGrainStorageOptions> configureOptions)
    {
        builder.Services.AddDapperGrainStorage(providerName, configureOptions);
        return builder;
    }

    public static IServiceCollection AddDapperGrainStorage(
        this IServiceCollection services,
        string providerName,
        Action<DapperGrainStorageOptions> configureOptions)
    {
        services.AddOptions<DapperGrainStorageOptions>(providerName)
            .Configure(configureOptions);

        services.AddTransient<
            IPostConfigureOptions<DapperGrainStorageOptions>,
            DefaultStorageProviderSerializerOptionsConfigurator<DapperGrainStorageOptions>>();

        services.AddKeyedSingleton<IGrainStorage>(
            providerName,
            (services, key) => DapperGrainStorageFactory.Create(services, key?.ToString() ?? providerName));

        services.AddKeyedSingleton<ILifecycleParticipant<ISiloLifecycle>>(
            providerName,
            (services, key) => (ILifecycleParticipant<ISiloLifecycle>)services.GetRequiredKeyedService<IGrainStorage>(key));

        return services;
    }
}
