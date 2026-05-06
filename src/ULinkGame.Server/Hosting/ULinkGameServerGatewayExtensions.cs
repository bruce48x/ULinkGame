using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ULinkGame.Server.Hosting;

public static class ULinkGameServerGatewayExtensions
{
    public static IServiceCollection AddULinkGameServerGateway(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ULinkRpcServersHostedService>());
        return services;
    }

    [Obsolete("Register project-specific options directly and call AddULinkGameServerGateway().")]
    public static IServiceCollection AddULinkGameServerGateway(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddULinkGameServerGateway();
    }

    public static IServiceCollection AddULinkRpcServer<TConfigurator>(this IServiceCollection services)
        where TConfigurator : class, IULinkRpcServerConfigurator
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IULinkRpcServerConfigurator, TConfigurator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ULinkRpcServersHostedService>());
        return services;
    }
}
