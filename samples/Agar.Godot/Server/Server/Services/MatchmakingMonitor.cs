using Orleans.Contracts.Matchmaking;
using Orleans.Contracts.Rooms;
using Orleans.Contracts.Sessions;
using Server.Realtime;
using Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace Server.Services;

internal sealed class MatchmakingMonitor
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private readonly IClusterClient _clusterClient;
    private readonly SessionDirectory _sessionDirectory;
    private readonly RoomRuntimeHost _roomRuntimeHost;
    private readonly GatewayNodeIdentity _gatewayNodeIdentity;
    private readonly ReliableMatchmakingPublisher _reliableMatchmakingPublisher;
    private readonly ILogger<MatchmakingMonitor> _logger;

    public MatchmakingMonitor(
        IClusterClient clusterClient,
        SessionDirectory sessionDirectory,
        RoomRuntimeHost roomRuntimeHost,
        GatewayNodeIdentity gatewayNodeIdentity,
        ReliableMatchmakingPublisher reliableMatchmakingPublisher,
        ILogger<MatchmakingMonitor> logger)
    {
        _clusterClient = clusterClient;
        _sessionDirectory = sessionDirectory;
        _roomRuntimeHost = roomRuntimeHost;
        _gatewayNodeIdentity = gatewayNodeIdentity;
        _reliableMatchmakingPublisher = reliableMatchmakingPublisher;
        _logger = logger;
    }

    public async Task WatchForRoomAssignmentAsync(string playerId, CancellationToken cancellationToken)
    {
        var sessionGrain = _clusterClient.GetGrain<IPlayerSessionGrain>(playerId);
        var matchmaking = _clusterClient.GetGrain<IMatchmakingGrain>("default");

        while (!cancellationToken.IsCancellationRequested)
        {
            var snapshot = await sessionGrain.GetSnapshotAsync().ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(snapshot.CurrentRoomId))
            {
                var room = await _clusterClient.GetGrain<IRoomGrain>(snapshot.CurrentRoomId)
                    .GetSnapshotAsync()
                    .ConfigureAwait(false);
                var player = room.Players.FirstOrDefault(entry => string.Equals(entry.UserId, playerId, StringComparison.Ordinal));
                _sessionDirectory.SetQueueTicket(playerId, null);
                _sessionDirectory.AssignRoom(playerId, snapshot.CurrentRoomId, snapshot.CurrentMatchId, player?.SeatIndex ?? -1);

                if (_gatewayNodeIdentity.IsRuntimeOwner(room.RuntimeGateway))
                {
                    await _roomRuntimeHost.EnsureRoomReadyAsync(room).ConfigureAwait(false);
                }

                await _reliableMatchmakingPublisher.PublishAsync(playerId, new MatchmakingStatusUpdate
                {
                    State = Shared.Interfaces.MatchmakingState.Matched,
                    QueueSize = room.MemberCount,
                    RoomCapacity = room.MaxPlayers,
                    RoomId = room.RoomId,
                    MatchedPlayerCount = room.MemberCount,
                    Message = $"Matched into room {room.RoomId}",
                    RealtimeConnection = GatewayEndpointMapper.ToRealtimeConnectionInfo(
                        room.RuntimeGateway,
                        room.RoomId,
                        room.MatchId,
                        snapshot.SessionToken)
                }).ConfigureAwait(false);

                return;
            }

            var status = await matchmaking.GetStatusAsync().ConfigureAwait(false);
            var position = status.PendingTickets.FindIndex(ticket => string.Equals(ticket.UserId, playerId, StringComparison.Ordinal));

            await _reliableMatchmakingPublisher.PublishAsync(playerId, new MatchmakingStatusUpdate
            {
                State = position >= 0 ? Shared.Interfaces.MatchmakingState.Queued : Shared.Interfaces.MatchmakingState.Searching,
                QueuePosition = position >= 0 ? position + 1 : 0,
                QueueSize = status.QueuedCount,
                RoomCapacity = status.DefaultRoomSize,
                RoomId = status.LastRoomId,
                MatchedPlayerCount = Math.Min(status.QueuedCount, status.DefaultRoomSize),
                Message = position >= 0 ? "Queued for matchmaking" : "Waiting for room assignment"
            }).ConfigureAwait(false);

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

}
