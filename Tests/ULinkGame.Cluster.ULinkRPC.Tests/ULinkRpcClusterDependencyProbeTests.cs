using ULinkGame.Cluster;
using ULinkGame.Cluster.ULinkRPC;
using ULinkRPC.Core;
using Xunit;

namespace ULinkGame.Cluster.ULinkRPC.Tests;

public sealed class ULinkRpcClusterDependencyProbeTests
{
    [Fact]
    public async Task CheckRouteDirectoryReturnsHealthyWhenResolveCompletes()
    {
        var probe = new ULinkRpcClusterDependencyProbe(
            new StaticClientFactory(new ResolvingClient()),
            TimeSpan.FromSeconds(1));

        var health = await probe.CheckRouteDirectoryAsync(
            NewDirectoryLocation(),
            TestContext.Current.CancellationToken);

        Assert.Equal("route-directory", health.Name);
        Assert.Equal(ULinkRpcClusterDependencyStatus.Healthy, health.Status);
        Assert.Null(health.Error);
    }

    [Fact]
    public async Task CheckRouteDirectoryReturnsTimeoutWithoutHanging()
    {
        var probe = new ULinkRpcClusterDependencyProbe(
            new StaticClientFactory(new HangingClient()),
            TimeSpan.FromMilliseconds(1));

        var health = await probe.CheckRouteDirectoryAsync(
            NewDirectoryLocation(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ULinkRpcClusterDependencyStatus.Timeout, health.Status);
        Assert.NotNull(health.Error);
    }

    [Fact]
    public async Task CheckRouteDirectoryReturnsUnhealthyWhenClientFactoryFails()
    {
        var probe = new ULinkRpcClusterDependencyProbe(
            new ThrowingClientFactory(new InvalidOperationException("connect failed")),
            TimeSpan.FromSeconds(1));

        var health = await probe.CheckRouteDirectoryAsync(
            NewDirectoryLocation(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ULinkRpcClusterDependencyStatus.Unhealthy, health.Status);
        Assert.Contains("connect failed", health.Error, StringComparison.Ordinal);
    }

    private static RouteLocation NewDirectoryLocation()
    {
        return new RouteLocation(
            "directory",
            "directory",
            new NodeEndpoint("tcp://127.0.0.1:21001"),
            DateTimeOffset.UtcNow.AddMinutes(1),
            nodeEpoch: 1,
            generation: 1);
    }

    private sealed class StaticClientFactory : IULinkRpcClusterClientFactory
    {
        private readonly IRpcClient _client;

        public StaticClientFactory(IRpcClient client)
        {
            _client = client;
        }

        public ValueTask<IRpcClient> GetClientAsync(
            RouteLocation target,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_client);
        }
    }

    private sealed class ThrowingClientFactory : IULinkRpcClusterClientFactory
    {
        private readonly Exception _exception;

        public ThrowingClientFactory(Exception exception)
        {
            _exception = exception;
        }

        public ValueTask<IRpcClient> GetClientAsync(
            RouteLocation target,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }

    private sealed class ResolvingClient : IRpcClient
    {
        public ValueTask<TResult> CallAsync<TArg, TResult>(
            RpcMethod<TArg, TResult> method,
            TArg? arg,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            object reply = new ULinkRpcRouteResolveReply();
            return ValueTask.FromResult((TResult)reply);
        }

        public void RegisterPushHandler<TArg>(
            RpcPushMethod<TArg> method,
            Action<TArg> handler)
        {
        }
    }

    private sealed class HangingClient : IRpcClient
    {
        public async ValueTask<TResult> CallAsync<TArg, TResult>(
            RpcMethod<TArg, TResult> method,
            TArg? arg,
            CancellationToken ct)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("unreachable");
        }

        public void RegisterPushHandler<TArg>(
            RpcPushMethod<TArg> method,
            Action<TArg> handler)
        {
        }
    }
}
