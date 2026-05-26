using System.Threading;
using System.Threading.Tasks;
using ULinkGame.Cluster;
using ULinkRPC.Core;

namespace ULinkGame.Cluster.ULinkRPC
{
    public interface IULinkRpcClusterTransportFactory
    {
        ValueTask<ITransport> ConnectAsync(
            RouteLocation target,
            ULinkRpcClusterEndpoint endpoint,
            CancellationToken cancellationToken = default);
    }
}
