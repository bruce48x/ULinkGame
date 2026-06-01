using ULinkGame.Cluster;
using ULinkGame.Server.Actors;
using Xunit;

namespace ULinkGame.Server.Tests;

public sealed class RemoteActorInvokerTests
{
    [Fact]
    public async Task TellAsync_maps_cluster_backpressure_to_remote_backpressure()
    {
        var invocation = CreateInvocation();
        var messenger = new RecordingNodeMessenger { Status = ClusterSendStatus.Backpressure };
        var invoker = CreateInvoker(invocation, nodeMessenger: messenger);

        var result = await invoker.TellAsync(invocation, TestContext.Current.CancellationToken);

        Assert.Equal(RemoteActorStatus.Backpressure, result.Status);
    }

    [Fact]
    public async Task TellAsync_maps_stale_route_to_node_unavailable()
    {
        var invocation = CreateInvocation();
        var messenger = new RecordingNodeMessenger { Status = ClusterSendStatus.StaleRoute };
        var invoker = CreateInvoker(invocation, nodeMessenger: messenger);

        var result = await invoker.TellAsync(invocation, TestContext.Current.CancellationToken);

        Assert.Equal(RemoteActorStatus.NodeUnavailable, result.Status);
    }

    [Fact]
    public async Task TellAsync_sends_envelope_without_reply_correlation()
    {
        var invocation = CreateInvocation();
        var messenger = new RecordingNodeMessenger();
        var invoker = CreateInvoker(invocation, nodeMessenger: messenger);

        var result = await invoker.TellAsync(invocation, TestContext.Current.CancellationToken);

        Assert.Equal(RemoteActorStatus.Accepted, result.Status);
        Assert.NotNull(messenger.LastMessage);
        Assert.True(ClusterActorEnvelope.TryFromClusterMessage(messenger.LastMessage, out var envelope));
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
        var invocation = CreateInvocation();
        var messenger = new RecordingNodeMessenger();
        var invoker = CreateInvoker(invocation, gateway, messenger);
        var replyPayload = new byte[] { 9, 8, 7 };
        messenger.OnSend = message =>
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
    public async Task AskAsync_send_failure_releases_pending_reply_immediately()
    {
        var gateway = new RemoteActorGateway();
        var invocation = CreateInvocation();
        var messenger = new RecordingNodeMessenger { Status = ClusterSendStatus.Backpressure };
        var invoker = CreateInvoker(invocation, gateway, messenger);

        var result = await invoker.AskAsync(invocation, TestContext.Current.CancellationToken);

        Assert.Equal(RemoteActorStatus.Backpressure, result.Status);

        var pending = gateway.RegisterPendingAsync(
            invocation.CorrelationId,
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        var replyPayload = new byte[] { 4, 5, 6 };

        await gateway.CreateReplyHandler().HandleAsync(
            new ClusterMessage(
                ClusterActorRouteKeys.ForReply(new NodeId("node-local")),
                RemoteActorGateway.ReplyKind,
                replyPayload,
                DateTimeOffset.UtcNow.AddSeconds(5),
                invocation.Node,
                invocation.CorrelationId),
            TestContext.Current.CancellationToken);

        var payload = await pending.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.Equal(replyPayload, payload.ToArray());
    }

    [Fact]
    public async Task TellAsync_sends_to_requested_node_directory_record()
    {
        var requestedNode = new NodeId("node-requested");
        var directory = new StubNodeDirectory
        {
            Record = CreateNodeRecord(
                clusterName: "local",
                node: requestedNode,
                endpoint: new NodeEndpoint("tcp://requested-node:21000"),
                nodeEpoch: 42)
        };
        var messenger = new RecordingNodeMessenger();
        var invoker = new RemoteActorInvoker(
            new RemoteActorGateway(),
            new NodeId("node-local"),
            directory,
            messenger,
            new RemoteActorOptions { ClusterName = "local", EndpointName = "cluster" });
        var invocation = CreateInvocation(node: requestedNode);

        var result = await invoker.TellAsync(invocation, TestContext.Current.CancellationToken);

        Assert.Equal(RemoteActorStatus.Accepted, result.Status);
        Assert.Equal("local", directory.LastClusterName);
        Assert.Equal(requestedNode, directory.LastNode);
        Assert.NotNull(messenger.LastTarget);
        Assert.Equal(requestedNode, messenger.LastTarget.Node);
        Assert.Equal(42, messenger.LastTarget.NodeEpoch);
        Assert.Equal("tcp://requested-node:21000", messenger.LastTarget.Endpoint.Address);
        Assert.Equal(ClusterActorRouteKeys.ForActor(invocation.ActorId.Value), messenger.LastTarget.Route);
        Assert.NotNull(messenger.LastMessage);
        Assert.True(ClusterActorEnvelope.TryFromClusterMessage(messenger.LastMessage, out var envelope));
        Assert.NotNull(envelope);
        Assert.Equal(invocation.ActorId.Value, envelope.ActorId);
    }

    [Fact]
    public async Task AskAsync_returns_expired_without_sending_when_deadline_has_passed()
    {
        var invocation = CreateInvocation(deadline: DateTimeOffset.UtcNow.AddSeconds(-1));
        var messenger = new RecordingNodeMessenger();
        var invoker = CreateInvoker(invocation, nodeMessenger: messenger);

        var result = await invoker.AskAsync(invocation, TestContext.Current.CancellationToken);

        Assert.Equal(RemoteActorStatus.Expired, result.Status);
        Assert.Null(messenger.LastMessage);
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

    private static RemoteActorInvocation CreateInvocation(
        DateTimeOffset? deadline = null,
        NodeId? node = null)
    {
        return new RemoteActorInvocation(
            node ?? new NodeId("node-b"),
            ActorId.From("room/1001"),
            "room",
            "leave",
            new byte[] { 1, 2, 3 },
            deadline ?? DateTimeOffset.UtcNow.AddSeconds(5),
            "corr-1");
    }

    private static RemoteActorInvoker CreateInvoker(
        RemoteActorInvocation invocation,
        RemoteActorGateway? gateway = null,
        RecordingNodeMessenger? nodeMessenger = null)
    {
        var directory = new StubNodeDirectory
        {
            Record = CreateNodeRecord(
                clusterName: "local",
                node: invocation.Node,
                endpoint: new NodeEndpoint("tcp://target-node:21000"),
                nodeEpoch: 1)
        };

        return new RemoteActorInvoker(
            gateway ?? new RemoteActorGateway(),
            new NodeId("node-local"),
            directory,
            nodeMessenger ?? new RecordingNodeMessenger(),
            new RemoteActorOptions { ClusterName = "local", EndpointName = "cluster" });
    }

    private static NodeRecord CreateNodeRecord(
        string clusterName,
        NodeId node,
        NodeEndpoint endpoint,
        long nodeEpoch)
    {
        return new NodeRecord(
            clusterName,
            node,
            nodeEpoch,
            new Dictionary<string, NodeEndpoint>(StringComparer.Ordinal)
            {
                ["cluster"] = endpoint
            },
            [new NodeServiceDescriptor("actor-host")],
            labels: null,
            NodeState.Ready,
            DateTimeOffset.UtcNow.AddMinutes(5),
            DateTimeOffset.UtcNow);
    }

    private sealed class StubNodeDirectory : INodeDirectory
    {
        public NodeRecord? Record { get; set; }

        public string? LastClusterName { get; private set; }

        public NodeId LastNode { get; private set; }

        public ValueTask<NodeRegistrationResult> RegisterAsync(
            NodeRegistration registration,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<NodeHeartbeatStatus> HeartbeatAsync(
            string clusterName,
            NodeId node,
            long nodeEpoch,
            DateTimeOffset leaseExpiresAt,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<NodeStateUpdateStatus> UpdateStateAsync(
            string clusterName,
            NodeId node,
            long nodeEpoch,
            NodeState state,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<NodeRecord?> ResolveAsync(
            string clusterName,
            NodeId node,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            LastClusterName = clusterName;
            LastNode = node;
            return ValueTask.FromResult(Record);
        }

        public ValueTask<IReadOnlyList<NodeRecord>> QueryAsync(
            NodeDirectoryQuery query,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<int> ExpireAsync(
            string clusterName,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingNodeMessenger : INodeMessenger
    {
        public RouteLocation? LastTarget { get; private set; }

        public ClusterMessage? LastMessage { get; private set; }

        public ClusterSendStatus Status { get; set; } = ClusterSendStatus.Accepted;

        public Action<ClusterMessage>? OnSend { get; set; }

        public ValueTask<ClusterSendStatus> SendAsync(
            RouteLocation target,
            ClusterMessage message,
            CancellationToken cancellationToken = default)
        {
            LastTarget = target;
            LastMessage = message;
            OnSend?.Invoke(message);
            return ValueTask.FromResult(Status);
        }
    }
}
