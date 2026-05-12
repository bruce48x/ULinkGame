using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Storage;

namespace ULinkGame.Sample.Silo.Persistence;

internal static class DapperGrainStorageFactory
{
    internal static IGrainStorage Create(IServiceProvider services, string name)
    {
        var optionsMonitor = services.GetRequiredService<IOptionsMonitor<DapperGrainStorageOptions>>();
        return ActivatorUtilities.CreateInstance<DapperGrainStorage>(
            services,
            name,
            optionsMonitor.Get(name));
    }
}
