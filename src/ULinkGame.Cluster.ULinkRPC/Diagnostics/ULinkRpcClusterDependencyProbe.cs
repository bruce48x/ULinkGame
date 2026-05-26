using System;
using System.Threading;
using System.Threading.Tasks;
using ULinkGame.Cluster;

namespace ULinkGame.Cluster.ULinkRPC
{
    public sealed class ULinkRpcClusterDependencyProbe
    {
        private static readonly RouteKey HealthRoute = new RouteKey("__ulinkgame/health__");

        private readonly IULinkRpcClusterClientFactory _clientFactory;
        private readonly TimeSpan _timeout;

        public ULinkRpcClusterDependencyProbe(
            IULinkRpcClusterClientFactory clientFactory,
            TimeSpan? timeout = null)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _timeout = timeout ?? TimeSpan.FromSeconds(2);
            if (_timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Health probe timeout must be positive.");
            }
        }

        public async ValueTask<ULinkRpcClusterDependencyHealth> CheckRouteDirectoryAsync(
            RouteLocation routeDirectory,
            CancellationToken cancellationToken = default)
        {
            if (routeDirectory is null)
            {
                throw new ArgumentNullException(nameof(routeDirectory));
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_timeout);

            try
            {
                var client = await _clientFactory.GetClientAsync(routeDirectory, timeout.Token).ConfigureAwait(false);
                var directory = new ULinkRpcRouteDirectory(client);
                await directory.ResolveAsync(HealthRoute, DateTimeOffset.UtcNow, timeout.Token).ConfigureAwait(false);
                return new ULinkRpcClusterDependencyHealth(
                    "route-directory",
                    ULinkRpcClusterDependencyStatus.Healthy);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new ULinkRpcClusterDependencyHealth(
                    "route-directory",
                    ULinkRpcClusterDependencyStatus.Timeout,
                    "Route directory health probe timed out.");
            }
            catch (Exception ex)
            {
                return new ULinkRpcClusterDependencyHealth(
                    "route-directory",
                    ULinkRpcClusterDependencyStatus.Unhealthy,
                    ex.Message);
            }
        }
    }
}
