using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ULinkGame.Server.Features;

public sealed class ULinkGameFeatureContext(
    IServiceCollection services,
    IConfiguration configuration,
    ULinkGameEndpointCatalog endpoints)
{
    public IServiceCollection Services { get; } = services;

    public IConfiguration Configuration { get; } = configuration;

    public ULinkGameEndpointCatalog Endpoints { get; } = endpoints;
}
