using ULinkGame.Cluster;
using ULinkGame.Server.Actors;
using Xunit;

namespace ULinkGame.Server.Tests;

public sealed class RemoteActorInvokerTests
{
    [Fact]
    public async Task TellAsync_maps_cluster_backpressure_to_remote_backpressure()
    {
        var router = new StubClusterRouter { Status = ClusterSendStatus.Backpressure };
        var invoker = new RemoteActorInvoker(router, new RemoteActorGateway(), new NodeId("node-local"));
        var invocation = CreateInvocation();

        var result = await invoker.TellAsync(invocation, TestContext.Current.CancellationToken);

        Assert.Equal(RemoteActorStatus.Backpressure, result.Status);
    }

    [Fact]
    public async Task TellAsync_maps_stale_route_to_node_unavailable()
    {
        var router = new StubClusterRouter { Status = ClusterSendStatus.StaleRoute };
        var invoker = new RemoteActorInvoker(router, new RemoteActorGateway(), new NodeId("node-local"));
        var invocation = CreateInvocation();

        var result = await invoker.TellAsync(invocation, TestContext.Current.CancellationToken);

        Assert.Equal(RemoteActorStatus.NodeUnavailable, result.Status);
    }

    [Fact]
    public async Task TellAsync_sends_envelope_without_reply_correlation()
    {
        var router = new StubClusterRouter();
        var invoker = new RemoteActorInvoker(router, new RemoteActorGateway(), new NodeId("node-local"));
        var invocation = CreateInvocation();

        var result = await invoker.TellAsync(invocation, TestContext.Current.CancellationToken);

        Assert.Equal(RemoteActorStatus.Accepted, result.Status);
        Assert.NotNull(router.LastMessage);
        Assert.True(ClusterActorEnvelope.TryFromClusterMessage(router.LastMessage, out var envelope));
        Assert.NotNull(envelope);
        Assert.Equal(invocation.ActorId.Value, envelope.ActorId);
        Assert.Equal(invocation.MethodName, envelope.Kind);
        Assert.Equal(invocation.Payload.ToArray(), envelope.Payload.ToArray());
        Assert.Equal(new NodeId("node-local"), envelope.SourceNode);
        Assert.Equal(invocation.CorrelationId, envelope.CorrelationId);
        Assert.Null(envelope.ReplyCorrelationId);
    }

    [Fact]
    public async Task AskAsync_sends_envelope_with_reply_correlation_and_returns_reply()
    {
        var gateway = new RemoteActorGateway();
        var router = new StubClusterRouter();
        var invoker = new RemoteActorInvoker(router, gateway, new NodeId("node-local"));
        var invocation = CreateInvocation();
        var replyPayload = new byte[] { 9, 8, 7 };
        router.OnSend = message =>
        {
            Assert.True(ClusterActorEnvelope.TryFromClusterMessage(message, out var envelope));
            Assert.NotNull(envelope);
            Assert.Equal(invocation.CorrelationId, envelope.ReplyCorrelationId);
            _ = gateway.CreateReplyHandler().HandleAsync(
                new ClusterMessage(
                    ClusterActorRouteKeys.ForReply(new NodeId("node-local")),
                    RemoteActorGateway.ReplyKind,
                    replyPayload,
                    DateTimeOffset.UtcNow.AddSeconds(5),
                    invocation.Node,
                    invocation.CorrelationId),
                TestContext.Current.CancellationToken);
        };

        var result = await invoker.AskAsync(invocation, TestContext.Current.CancellationToken);

        Assert.Equal(RemoteActorStatus.Replied, result.Status);
        Assert.Equal(replyPayload, result.Payload.ToArray());
    }

    [Fact]
    public async Task AskAsync_returns_expired_without_sending_when_deadline_has_passed()
    {
        var router = new StubClusterRouter();
        var invoker = new RemoteActorInvoker(router, new RemoteActorGateway(), new NodeId("node-local"));
        var invocation = CreateInvocation(deadline: DateTimeOffset.UtcNow.AddSeconds(-1));

        var result = await invoker.AskAsync(invocation, TestContext.Current.CancellationToken);

        Assert.Equal(RemoteActorStatus.Expired, result.Status);
        Assert.Null(router.LastMessage);
    }

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

    private static RemoteActorInvocation CreateInvocation(DateTimeOffset? deadline = null)
    {
        return new RemoteActorInvocation(
            new NodeId("node-b"),
            ActorId.From("room/1001"),
            "room",
            "leave",
            new byte[] { 1, 2, 3 },
            deadline ?? DateTimeOffset.UtcNow.AddSeconds(5),
            "corr-1");
    }

    private sealed class StubClusterRouter : IClusterRouter
    {
        public ClusterSendStatus Status { get; set; } = ClusterSendStatus.Accepted;

        public ClusterMessage? LastMessage { get; private set; }

        public Action<ClusterMessage>? OnSend { get; set; }

        public ValueTask<ClusterSendStatus> SendAsync(
            ClusterMessage message,
            CancellationToken cancellationToken = default)
        {
            LastMessage = message;
            OnSend?.Invoke(message);
            return ValueTask.FromResult(Status);
        }
    }
}
