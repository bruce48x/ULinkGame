using System.Threading;
using System.Threading.Tasks;

namespace ULinkGame.Cluster
{
    public interface INodeMessenger
    {
        ValueTask<ClusterSendStatus> SendAsync(
            NodeId target,
            ClusterMessage message,
            CancellationToken cancellationToken = default);
    }
}
