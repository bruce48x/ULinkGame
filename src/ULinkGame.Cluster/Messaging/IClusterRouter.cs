using System.Threading;
using System.Threading.Tasks;

namespace ULinkGame.Cluster
{
    public interface IClusterRouter
    {
        ValueTask<ClusterSendStatus> SendAsync(
            ClusterMessage message,
            CancellationToken cancellationToken = default);
    }
}
