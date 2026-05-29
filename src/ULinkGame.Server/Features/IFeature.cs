using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ULinkGame.Server.Features;

public interface IFeature
{
    void Configure(IServiceCollection services, IConfiguration config);
}
