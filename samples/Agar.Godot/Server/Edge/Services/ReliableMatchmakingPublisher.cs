using Shared.Interfaces;
using ULinkGame.Server.ReliablePush;
using Microsoft.Extensions.Logging;

namespace Edge.Services;

internal sealed class ReliableMatchmakingPublisher
{
    private readonly IReliablePushOutbox _reliablePushOutbox;
    private readonly SessionDirectory _sessionDirectory;
    private readonly ILogger<ReliableMatchmakingPublisher> _logger;

    public ReliableMatchmakingPublisher(
        IReliablePushOutbox reliablePushOutbox,
        SessionDirectory sessionDirectory,
        ILogger<ReliableMatchmakingPublisher> logger)
    {
        _reliablePushOutbox = reliablePushOutbox;
        _sessionDirectory = sessionDirectory;
        _logger = logger;
    }

    public async ValueTask PublishAsync(string playerId, MatchmakingStatusUpdate update, CancellationToken cancellationToken = default)
    {
        await _reliablePushOutbox.PublishAsync(
            playerId,
            ReliablePushKinds.MatchmakingStatus,
            Clone(update),
            DeliverAsync,
            cancellationToken).ConfigureAwait(false);
    }

    public ValueTask ReplayPendingAsync(string playerId, CancellationToken cancellationToken = default)
    {
        return _reliablePushOutbox.ReplayPendingAsync(playerId, DeliverAsync, cancellationToken);
    }

    private ValueTask DeliverAsync(ReliablePushRecord record)
    {
        if (!string.Equals(record.Kind, ReliablePushKinds.MatchmakingStatus, StringComparison.Ordinal) ||
            record.Payload is not MatchmakingStatusUpdate update)
        {
            return ValueTask.CompletedTask;
        }

        var registration = _sessionDirectory.Get(record.OwnerKey);
        if (registration?.ControlCallback is null)
        {
            return ValueTask.CompletedTask;
        }

        var payload = Clone(update);
        payload.ReliableSequence = record.Sequence;
        SafeInvoke(registration.ControlCallback, callback => callback.OnMatchmakingStatus(payload));
        return ValueTask.CompletedTask;
    }

    private void SafeInvoke(IPlayerCallback callback, Action<IPlayerCallback> action)
    {
        try
        {
            action(callback);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push reliable matchmaking callback.");
        }
    }

    private static MatchmakingStatusUpdate Clone(MatchmakingStatusUpdate source)
    {
        return new MatchmakingStatusUpdate
        {
            State = source.State,
            Message = source.Message,
            RoomId = source.RoomId,
            QueuePosition = source.QueuePosition,
            QueueSize = source.QueueSize,
            RoomCapacity = source.RoomCapacity,
            MatchedPlayerCount = source.MatchedPlayerCount,
            ReliableSequence = source.ReliableSequence,
            RealtimeConnection = source.RealtimeConnection is null
                ? null
                : new RealtimeConnectionInfo
                {
                    Transport = source.RealtimeConnection.Transport,
                    Host = source.RealtimeConnection.Host,
                    Port = source.RealtimeConnection.Port,
                    Path = source.RealtimeConnection.Path,
                    RoomId = source.RealtimeConnection.RoomId,
                    MatchId = source.RealtimeConnection.MatchId,
                    SessionToken = source.RealtimeConnection.SessionToken
                }
        };
    }
}
