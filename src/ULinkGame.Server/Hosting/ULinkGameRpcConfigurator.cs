using ULinkGame.Server.Configuration;
using ULinkRPC.Core;
using ULinkRPC.Server;

namespace ULinkGame.Server.Hosting;

internal sealed class ULinkGameRpcConfigurator : IULinkRpcServerConfigurator
{
    private readonly ServerRpcServerOptions _options;
    private readonly Func<IRpcSerializer> _serializerFactory;
    private readonly Func<ServerRpcServerOptions, Task<IRpcConnectionAcceptor>> _acceptorFactory;
    private readonly Action<RpcServiceRegistry>? _bindServices;

    public ULinkGameRpcConfigurator(
        ServerRpcServerOptions options,
        Func<IRpcSerializer> serializerFactory,
        Func<ServerRpcServerOptions, Task<IRpcConnectionAcceptor>> acceptorFactory,
        Action<RpcServiceRegistry>? bindServices)
    {
        _options = options;
        _serializerFactory = serializerFactory;
        _acceptorFactory = acceptorFactory;
        _bindServices = bindServices;
    }

    public string Name { get; init; } = "default";

    public void Configure(ULinkGameServerRpcContext context)
    {
        var builder = context.Builder;
        builder.UseSerializer(_serializerFactory());
        builder.UseAcceptor(async ct => await _acceptorFactory(_options));
        _bindServices?.Invoke(builder.ServiceRegistry);
    }
}
