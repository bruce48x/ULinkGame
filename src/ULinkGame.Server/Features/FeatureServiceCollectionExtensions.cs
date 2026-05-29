using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
}
