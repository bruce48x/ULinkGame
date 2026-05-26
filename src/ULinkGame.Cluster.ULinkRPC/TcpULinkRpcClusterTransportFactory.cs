using System.Threading;
using System.Threading.Tasks;
using ULinkGame.Cluster;
using ULinkRPC.Core;
using ULinkRPC.Transport.Tcp;

namespace ULinkGame.Cluster.ULinkRPC
{
    public sealed class TcpULinkRpcClusterTransportFactory : IULinkRpcClusterTransportFactory
    {
        public async ValueTask<ITransport> ConnectAsync(
            RouteLocation target,
            ULinkRpcClusterEndpoint endpoint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var transport = new TcpTransport(endpoint.Host, endpoint.Port);
            await transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
            return transport;
        }
    }
}
