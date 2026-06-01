using ULinkGame.Cluster;
using ULinkGame.Server.Actors;
using Xunit;

namespace ULinkGame.Server.Tests;

public sealed class RemoteActorInvokerTests
{
    [Fact]
    public void RemoteActorInvocation_copies_payload()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var invocation = new RemoteActorInvocation(
            new NodeId("node-a"),
            ActorId.From("room/1001"),
            "room",
            "join",
            bytes,
            DateTimeOffset.UtcNow.AddSeconds(10),
            "corr-1");

        bytes[0] = 9;

        Assert.Equal(1, invocation.Payload.ToArray()[0]);
    }

    [Fact]
    public void RemoteActorInvocationResult_copies_payload()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var result = RemoteActorInvocationResult.Replied(bytes);

        bytes[0] = 9;

        Assert.Equal(1, result.Payload.ToArray()[0]);
    }

    [Theory]
    [InlineData(typeof(RemoteActorInvocation))]
    [InlineData(typeof(RemoteActorInvocationResult))]
    public void RemoteActor_payload_has_no_public_setter(Type type)
    {
        var payload = type.GetProperty(nameof(RemoteActorInvocation.Payload));

        Assert.NotNull(payload);
        Assert.Null(payload.SetMethod);
    }

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
