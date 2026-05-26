using System.Threading;
using System.Threading.Tasks;

namespace ULinkGame.Cluster
{
    public interface IClusterMessageHandler
    {
        ValueTask<ClusterSendStatus> HandleAsync(
            ClusterMessage message,
            CancellationToken cancellationToken = default);
    }
}
