using System.Threading;
using System.Threading.Tasks;
using ULinkGame.Cluster;
using ULinkRPC.Core;

namespace ULinkGame.Cluster.ULinkRPC
{
    public interface IULinkRpcClusterClientFactory
    {
        ValueTask<IRpcClient> GetClientAsync(
            RouteLocation target,
            CancellationToken cancellationToken = default);
    }
}
