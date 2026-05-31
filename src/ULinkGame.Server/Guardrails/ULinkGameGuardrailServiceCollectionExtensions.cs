using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ULinkGame.Server.Guardrails.Rules;

namespace ULinkGame.Server.Guardrails;

public static class ULinkGameGuardrailServiceCollectionExtensions
{
    public static IServiceCollection AddULinkGameRuntimeValidation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IULinkGameValidationRule, NodeIdentityRule>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IULinkGameValidationRule, EndpointRule>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IULinkGameValidationRule, HotfixSourceRule>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IULinkGameValidationRule, ClusterServiceGraphRule>());
        services.TryAddSingleton<ULinkGameRuntimeValidator>();

        return services;
    }
}
