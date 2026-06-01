using ULinkGame.Cluster;
using ULinkGame.Server.Actors;
using Xunit;

namespace ULinkGame.Server.Tests;

public sealed class RemoteActorInvokerTests
{
    [Fact]
    public void RemoteActorException_preserves_structured_failure_fields()
    {
        var exception = new RemoteActorException(
            RemoteActorStatus.RouteNotFound,
            ActorId.From("room/1001"),
            "room",
            "join",
            "The route was not found.",
            new NodeId("node-a"),
            "corr-1");

        Assert.Equal(RemoteActorStatus.RouteNotFound, exception.Status);
        Assert.Equal(ActorId.From("room/1001"), exception.ActorId);
        Assert.Equal("room", exception.ActorName);
        Assert.Equal("join", exception.MethodName);
        Assert.Equal(new NodeId("node-a"), exception.Node);
        Assert.Equal("corr-1", exception.CorrelationId);
        Assert.Contains("RouteNotFound", exception.Message);
    }
}
