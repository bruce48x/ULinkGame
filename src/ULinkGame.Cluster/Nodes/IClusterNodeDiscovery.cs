using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ULinkGame.Cluster
{
    public interface IClusterNodeDiscovery
    {
        ValueTask<IReadOnlyList<ClusterNodeDescriptor>> ListAsync(
            ClusterFeature feature,
            CancellationToken cancellationToken = default);

        ValueTask<NodeId?> AnyAsync(
            ClusterFeature feature,
            CancellationToken cancellationToken = default);
    }
}
