using Microsoft.Extensions.DependencyInjection;
using Gateway.Generated;
using Gateway.Services;
using ULinkGame.Server.Configuration;
using ULinkGame.Server.Hosting;
using ULinkRPC.Serializer.MemoryPack;
using ULinkRPC.Transport.WebSocket;

namespace Gateway.Hosting;

internal sealed class DefaultControlPlaneRpcServerConfigurator : IULinkRpcServerConfigurator
{
    private readonly ServerRpcServerOptions _options;

    public DefaultControlPlaneRpcServerConfigurator(ServerRpcServerOptions options)
    {
        _options = options;
    }

    public string Name => "control";

    public void Configure(ULinkGameServerRpcContext context)
    {
        var builder = context.Builder;
        var path = string.IsNullOrWhiteSpace(_options.Path) ? "/ws" : _options.Path;

        builder
            .UseSerializer(new MemoryPackRpcSerializer())
            .UseAcceptor(async ct => await WsConnectionAcceptor.CreateAsync(builder.ResolvePort(_options.Port), path, ct));

        PlayerServiceBinder.Bind(
            builder.ServiceRegistry,
            callback => ActivatorUtilities.CreateInstance<PlayerService>(context.Services, callback));
    }
}
