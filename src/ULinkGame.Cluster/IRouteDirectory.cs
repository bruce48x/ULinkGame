using System;
using System.Threading;
using System.Threading.Tasks;

namespace ULinkGame.Cluster
{
    public interface IRouteDirectory
    {
        ValueTask RegisterAsync(
            RouteLocation location,
            CancellationToken cancellationToken = default);

        ValueTask<RouteLocation?> ResolveAsync(
            RouteKey route,
            DateTimeOffset now,
            CancellationToken cancellationToken = default);

        ValueTask<int> ExpireAsync(
            DateTimeOffset now,
            CancellationToken cancellationToken = default);

        ValueTask<int> ClearByNodeAsync(
            NodeId node,
            CancellationToken cancellationToken = default);
    }
}
