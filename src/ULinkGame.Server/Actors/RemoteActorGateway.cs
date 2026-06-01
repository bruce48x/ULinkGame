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

        var registration = new PendingRegistration(tcs, _pending, correlationId);
        var timeoutTimer = new Timer(static state =>
        {
            var pending = (PendingRegistration)state!;
            if (pending.Requests.TryRemove(pending.CorrelationId, out _))
            {
                pending.Completion.TrySetException(new TimeoutException(
                    $"No reply received for correlation id '{pending.CorrelationId}' within the timeout."));
            }
        }, registration, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        registration.TimeoutTimer = timeoutTimer;
        timeoutTimer.Change(timeout, Timeout.InfiniteTimeSpan);

        if (cancellationToken.CanBeCanceled)
        {
            registration.Cancellation = cancellationToken.Register(static state =>
            {
                var pending = (PendingRegistration)state!;
                if (pending.Requests.TryRemove(pending.CorrelationId, out _))
                {
                    pending.Completion.TrySetCanceled();
                }
            }, registration);
        }

        tcs.Task.ContinueWith(static (_, state) =>
        {
            ((PendingRegistration)state!).Dispose();
        }, registration, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);

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

    private sealed class PendingRegistration : IDisposable
    {
        public PendingRegistration(
            TaskCompletionSource<ReadOnlyMemory<byte>> completion,
            ConcurrentDictionary<string, TaskCompletionSource<ReadOnlyMemory<byte>>> requests,
            string correlationId)
        {
            Completion = completion;
            Requests = requests;
            CorrelationId = correlationId;
        }

        public TaskCompletionSource<ReadOnlyMemory<byte>> Completion { get; }

        public ConcurrentDictionary<string, TaskCompletionSource<ReadOnlyMemory<byte>>> Requests { get; }

        public string CorrelationId { get; }

        public Timer? TimeoutTimer { get; set; }

        public CancellationTokenRegistration Cancellation { get; set; }

        public void Dispose()
        {
            TimeoutTimer?.Dispose();
            Cancellation.Dispose();
        }
    }
}
