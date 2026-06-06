using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ULinkGame.Server.Configuration;
using ULinkGame.Server.Features;
using ULinkRPC.Core;
using ULinkRPC.Server;

namespace ULinkGame.Server.Hosting;

public sealed class ULinkGameServerBuilder
{
    private Func<IRpcSerializer>? _serializerFactory;
    private Func<ServerRpcServerOptions, Task<IRpcConnectionAcceptor>>? _acceptorFactory;
    private Action<RpcServiceRegistry>? _serviceBinder;
    private string _transport = "websocket";
    private Action<ULinkGameFeatureCatalogBuilder>? _configureFeatures;
    private readonly List<Action<IServiceCollection>> _serviceRegistrations = new();
    private readonly List<Action<IConfigurationBuilder>> _configActions = new();
    private readonly List<RpcEndpointRegistration> _additionalEndpoints = new();

    internal IHostApplicationBuilder HostBuilder { get; }

    internal ULinkGameServerBuilder(IHostApplicationBuilder hostBuilder)
    {
        HostBuilder = hostBuilder;
    }

    public ULinkGameServerBuilder AddServices(Action<IServiceCollection> register)
    {
        ArgumentNullException.ThrowIfNull(register);
        _serviceRegistrations.Add(register);
        return this;
    }

    public ULinkGameServerBuilder ConfigureAppConfiguration(Action<IConfigurationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _configActions.Add(configure);
        return this;
    }

    public ULinkGameServerBuilder UseTransport(string transport)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transport);
        _transport = transport;
        return this;
    }

    public ULinkGameServerBuilder ConfigureFeatures(Action<ULinkGameFeatureCatalogBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _configureFeatures = configure;
        return this;
    }

    public ULinkGameServerBuilder UseSerializer(Func<IRpcSerializer> factory)
    {
        _serializerFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        return this;
    }

    public ULinkGameServerBuilder UseAcceptor(Func<ServerRpcServerOptions, Task<IRpcConnectionAcceptor>> factory)
    {
        _acceptorFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        return this;
    }

    public ULinkGameServerBuilder BindServices(Action<RpcServiceRegistry> bind)
    {
        _serviceBinder = bind ?? throw new ArgumentNullException(nameof(bind));
        return this;
    }

    public ULinkGameServerBuilder AddRpcEndpoint(
        string name,
        string transport,
        Func<IRpcSerializer> serializerFactory,
        Func<ServerRpcServerOptions, Task<IRpcConnectionAcceptor>> acceptorFactory,
        Action<RpcServiceRegistry>? serviceBinder = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(transport);
        ArgumentNullException.ThrowIfNull(serializerFactory);
        ArgumentNullException.ThrowIfNull(acceptorFactory);

        _additionalEndpoints.Add(new RpcEndpointRegistration(
            name, transport, serializerFactory, acceptorFactory, serviceBinder));
        return this;
    }

    internal void ApplyToHostBuilder()
    {
        foreach (var register in _serviceRegistrations)
        {
            register(HostBuilder.Services);
        }
    }

    internal Func<IRpcSerializer> GetSerializerFactory()
    {
        return _serializerFactory ?? throw new InvalidOperationException(
            "Serializer factory is required. Call UseSerializer() before RunAsync().");
    }

    internal Func<ServerRpcServerOptions, Task<IRpcConnectionAcceptor>> GetAcceptorFactory()
    {
        return _acceptorFactory ?? throw new InvalidOperationException(
            "Acceptor factory is required. Call UseAcceptor() before RunAsync().");
    }

    internal Action<RpcServiceRegistry>? GetServiceBinder() => _serviceBinder;

    internal string GetTransport() => _transport;

    internal Action<ULinkGameFeatureCatalogBuilder>? GetFeatureConfiguration() => _configureFeatures;

    internal IReadOnlyList<RpcEndpointRegistration> GetAdditionalEndpoints() => _additionalEndpoints;

    internal sealed record RpcEndpointRegistration(
        string Name,
        string Transport,
        Func<IRpcSerializer> SerializerFactory,
        Func<ServerRpcServerOptions, Task<IRpcConnectionAcceptor>> AcceptorFactory,
        Action<RpcServiceRegistry>? ServiceBinder);
}
