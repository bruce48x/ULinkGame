using System.Collections.Concurrent;
using ULinkGame.Cluster;

namespace ULinkGame.Server.Actors;

public sealed class RemoteActorGateway
{
    public const string ReplyKind = "_actor_reply";

    private readonly ConcurrentDictionary<string, TaskCompletionSource<ReadOnlyMemory<byte>>> _pending = new();

    public IClusterMessageHandler CreateReplyHandler()
    {
        return new ReplyHandler(this);
    }

    public Task<ReadOnlyMemory<byte>> RegisterPendingAsync(
        string correlationId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<ReadOnlyMemory<byte>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_pending.TryAdd(correlationId, tcs))
        {
            throw new InvalidOperationException(
                $"A pending request with correlation id '{correlationId}' already exists.");
        }

        cancellationToken.Register(static state =>
        {
            var (tcs, dict, key) = (Tuple<TaskCompletionSource<ReadOnlyMemory<byte>>,
                ConcurrentDictionary<string, TaskCompletionSource<ReadOnlyMemory<byte>>>,
                string>)state!;
            if (dict.TryRemove(key, out _))
            {
                tcs.TrySetCanceled();
            }
        }, Tuple.Create(tcs, _pending, correlationId));

        using var timeoutCts = new CancellationTokenSource(timeout);
        timeoutCts.Token.Register(static state =>
        {
            var (tcs, dict, key) = (Tuple<TaskCompletionSource<ReadOnlyMemory<byte>>,
                ConcurrentDictionary<string, TaskCompletionSource<ReadOnlyMemory<byte>>>,
                string>)state!;
            if (dict.TryRemove(key, out _))
            {
                tcs.TrySetException(new TimeoutException(
                    $"No reply received for correlation id '{key}' within the timeout."));
            }
        }, Tuple.Create(tcs, _pending, correlationId));

        return tcs.Task;
    }

    public static async ValueTask SendReplyAsync(
        IClusterRouter router,
        NodeId sourceNode,
        string correlationId,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(router);

        var replyMessage = new ClusterMessage(
            ClusterActorRouteKeys.ForReply(sourceNode),
            ReplyKind,
            payload,
            DateTimeOffset.UtcNow.AddSeconds(30),
            sourceNode,
            correlationId);

        await router.SendAsync(replyMessage, cancellationToken).ConfigureAwait(false);
    }

    private sealed class ReplyHandler : IClusterMessageHandler
    {
        private readonly RemoteActorGateway _gateway;

        public ReplyHandler(RemoteActorGateway gateway)
        {
            _gateway = gateway;
        }

        public ValueTask<ClusterSendStatus> HandleAsync(
            ClusterMessage message,
            CancellationToken cancellationToken)
        {
            if (message.Kind != ReplyKind)
            {
                return ValueTask.FromResult(ClusterSendStatus.RouteNotFound);
            }

            if (message.CorrelationId is not null &&
                _gateway._pending.TryRemove(message.CorrelationId, out var tcs))
            {
                tcs.TrySetResult(message.Payload);
            }

            return ValueTask.FromResult(ClusterSendStatus.Accepted);
        }
    }
}
