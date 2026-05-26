using ULinkGame.Cluster;
using Xunit;

namespace ULinkGame.Cluster.Tests;

public sealed class InMemoryRouteDirectoryTests
{
    [Fact]
    public async Task RegisterReplacesExistingRouteLocation()
    {
        var directory = new InMemoryRouteDirectory();
        var now = DateTimeOffset.UtcNow;
        var route = new RouteKey("room/1");

        await directory.RegisterAsync(
            new RouteLocation(route, "node-a", new NodeEndpoint("in-memory://node-a"), now.AddMinutes(1)),
            TestContext.Current.CancellationToken);
        await directory.RegisterAsync(
            new RouteLocation(route, "node-b", new NodeEndpoint("in-memory://node-b"), now.AddMinutes(1)),
            TestContext.Current.CancellationToken);

        var resolved = await directory.ResolveAsync(route, now, TestContext.Current.CancellationToken);

        Assert.NotNull(resolved);
        Assert.Equal(new NodeId("node-b"), resolved.Node);
    }

    [Fact]
    public async Task ExpiredRouteIsUnavailableAndRemoved()
    {
        var directory = new InMemoryRouteDirectory();
        var now = DateTimeOffset.UtcNow;
        var route = new RouteKey("room/1");
        await directory.RegisterAsync(
            new RouteLocation(route, "node-a", new NodeEndpoint("in-memory://node-a"), now.AddSeconds(1)),
            TestContext.Current.CancellationToken);

        var resolved = await directory.ResolveAsync(route, now.AddSeconds(2), TestContext.Current.CancellationToken);
        var expiredAgain = await directory.ExpireAsync(now.AddSeconds(2), TestContext.Current.CancellationToken);

        Assert.Null(resolved);
        Assert.Equal(0, expiredAgain);
    }

    [Fact]
    public async Task ClearByNodeRemovesOnlyMatchingNodeRoutes()
    {
        var directory = new InMemoryRouteDirectory();
        var now = DateTimeOffset.UtcNow;
        await directory.RegisterAsync(
            new RouteLocation("room/1", "node-a", new NodeEndpoint("in-memory://node-a"), now.AddMinutes(1)),
            TestContext.Current.CancellationToken);
        await directory.RegisterAsync(
            new RouteLocation("room/2", "node-b", new NodeEndpoint("in-memory://node-b"), now.AddMinutes(1)),
            TestContext.Current.CancellationToken);

        var removed = await directory.ClearByNodeAsync("node-a", TestContext.Current.CancellationToken);

        Assert.Equal(1, removed);
        Assert.Null(await directory.ResolveAsync("room/1", now, TestContext.Current.CancellationToken));
        Assert.NotNull(await directory.ResolveAsync("room/2", now, TestContext.Current.CancellationToken));
    }
}
