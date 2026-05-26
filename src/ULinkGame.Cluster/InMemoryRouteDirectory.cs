using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ULinkGame.Cluster
{
    public sealed class InMemoryRouteDirectory : IRouteDirectory
    {
        private readonly object _gate = new object();
        private readonly Dictionary<RouteKey, RouteLocation> _routes = new Dictionary<RouteKey, RouteLocation>();

        public ValueTask RegisterAsync(
            RouteLocation location,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                _routes[location.Route] = location;
            }

            return default;
        }

        public ValueTask<RouteLocation?> ResolveAsync(
            RouteKey route,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                if (!_routes.TryGetValue(route, out var location))
                {
                    return new ValueTask<RouteLocation?>((RouteLocation?)null);
                }

                if (location.IsExpired(now))
                {
                    _routes.Remove(route);
                    return new ValueTask<RouteLocation?>((RouteLocation?)null);
                }

                return new ValueTask<RouteLocation?>(location);
            }
        }

        public ValueTask<int> ExpireAsync(
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                var expired = _routes
                    .Where(route => route.Value.IsExpired(now))
                    .Select(route => route.Key)
                    .ToArray();

                foreach (var route in expired)
                {
                    _routes.Remove(route);
                }

                return new ValueTask<int>(expired.Length);
            }
        }

        public ValueTask<int> ClearByNodeAsync(
            NodeId node,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                var stale = _routes
                    .Where(route => route.Value.Node == node)
                    .Select(route => route.Key)
                    .ToArray();

                foreach (var route in stale)
                {
                    _routes.Remove(route);
                }

                return new ValueTask<int>(stale.Length);
            }
        }
    }
}
