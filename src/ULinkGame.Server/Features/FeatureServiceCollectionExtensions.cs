using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ULinkGame.Server.Configuration;

namespace ULinkGame.Server.Features;

public static class FeatureServiceCollectionExtensions
{
    public static IServiceCollection AddFeatures(
        this IServiceCollection services,
        IConfiguration config,
        Action<FeatureBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new FeatureBuilder();

        var filter = config.GetSection("ULinkGame:Features").Get<FeatureFilter>();
        if (filter is not null)
        {
            builder.UseFilter(filter);
        }

        configure(builder);

        foreach (var feature in builder.ResolveFeatures())
        {
            feature.Configure(services, config);
        }

        return services;
    }

    public static IServiceCollection AddULinkGame(
        this IServiceCollection services,
        IConfiguration config,
        Action<ULinkGameFeatureCatalogBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(configure);

        var options = ULinkGameRuntimeOptions.FromConfiguration(config);
        var builder = new ULinkGameFeatureCatalogBuilder();
        configure(builder);

        var catalog = builder.Build(options);
        var endpointCatalog = new ULinkGameEndpointCatalog(options.Endpoints);
        var context = new ULinkGameFeatureContext(services, config, endpointCatalog);

        services.AddSingleton(options);
        services.AddSingleton(catalog);
        services.AddSingleton(endpointCatalog);

        foreach (var definition in catalog.ActiveDefinitions)
        {
            var feature = CreateFeature(definition);
            feature.ConfigureServices(context);
        }

        return services;
    }

    private static ULinkGameFeature CreateFeature(ULinkGameFeatureDefinition definition)
    {
        try
        {
            return (ULinkGameFeature?)Activator.CreateInstance(definition.ImplementationType)
                ?? throw new InvalidOperationException(
                    $"ULinkGame feature '{definition.Name}' ({definition.ImplementationType.FullName}) could not be created.");
        }
        catch (MissingMethodException ex)
        {
            throw new InvalidOperationException(
                $"ULinkGame feature '{definition.Name}' ({definition.ImplementationType.FullName}) must have a public parameterless constructor. Pass dependencies through ULinkGameFeatureContext instead.",
                ex);
        }
    }
}
