using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ULinkGame.Server.Features;

public abstract class ULinkGameFeature
{
    public virtual void ConfigureServices(ULinkGameFeatureContext context)
    {
    }
}
