using Orleans.Contracts.Users;
using Orleans.Contracts.Leaderboard;
using Orleans.Contracts;
using Orleans.Contracts.Sessions;
using Edge.Realtime;
using Shared.Interfaces;
using ULinkGame.Server.ReliablePush;
using ULinkGame.Server.Sessions;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Edge.Services;

internal sealed class PlayerService : IPlayerService, IDisposable, IAsyncDisposable
{
    private readonly IClusterClient _clusterClient;
    private readonly IPlayerCallback _callback;
    private readonly SessionDirectory _sessionDirectory;
    private readonly EdgeMatchmakingCoordinator _matchmakingCoordinator;
    private readonly EdgeNodeIdentity _edgeNodeIdentity;
    private readonly RoomRuntimeHost _roomRuntimeHost;
    private readonly ReliableMatchmakingPublisher _reliableMatchmakingPublisher;
    private readonly IReliablePushOutbox _reliablePushOutbox;
    private readonly IReliablePushAckService _reliablePushAckService;
    private readonly ILogger<PlayerService> _logger;
    private bool _disposed;
    private string? _playerId;
    private string? _connectionId;
    private bool _isRealtimeConnection;
    private bool _controlLoggedIn;

    public PlayerService(
        IPlayerCallback callback,
        IClusterClient clusterClient,
        SessionDirectory sessionDirectory,
        EdgeMatchmakingCoordinator matchmakingCoordinator,
        EdgeNodeIdentity edgeNodeIdentity,
        RoomRuntimeHost roomRuntimeHost,
        ReliableMatchmakingPublisher reliableMatchmakingPublisher,
        IReliablePushOutbox reliablePushOutbox,
        IReliablePushAckService reliablePushAckService,
        ILogger<PlayerService> logger)
    {
        _callback = callback;
        _clusterClient = clusterClient;
        _sessionDirectory = sessionDirectory;
        _matchmakingCoordinator = matchmakingCoordinator;
        _edgeNodeIdentity = edgeNodeIdentity;
        _roomRuntimeHost = roomRuntimeHost;
        _reliableMatchmakingPublisher = reliableMatchmakingPublisher;
        _reliablePushOutbox = reliablePushOutbox;
        _reliablePushAckService = reliablePushAckService;
        _logger = logger;
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        DisposeAsyncCore().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    public async ValueTask<LoginReply> LoginAsync(LoginRequest req)
    {
        ThrowIfDisposed();

        var account = req.Account;
        var password = req.Password;
        if (req.GuestLogin)
        {
            account = CreateGuestAccount();
            password = CreateGuestPassword();
        }

        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(password))
        {
            return new LoginReply { Code = LoginResultCodes.InvalidRequest, Message = "Login request is incomplete." };
        }

        UserLoginResult loginResult;
        try
        {
            loginResult = await _clusterClient.GetGrain<IUserGrain>(account)
                .LoginAsync(password, req.Reconnect)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Login rejected for account {Account}.", account);
            return new LoginReply { Code = LoginResultCodes.Rejected, Message = "Login rejected." };
        }

        _playerId = loginResult.UserId;
        _connectionId = Guid.NewGuid().ToString("N");
        _controlLoggedIn = true;

        var sessionGrain = _clusterClient.GetGrain<IPlayerSessionGrain>(loginResult.UserId);
        GameSessionKey sessionKey;
        if (req.Reconnect)
        {
            var resumeDecision = await _sessionDirectory
                .ResumeControlAsync(loginResult.UserId, loginResult.SessionToken, _connectionId, _callback)
                .ConfigureAwait(false);
            if (resumeDecision.Status != SessionResumeStatus.Resumed || resumeDecision.Session is null)
            {
                _playerId = null;
                _connectionId = null;
                _controlLoggedIn = false;
                return new LoginReply
                {
                    Code = LoginResultCodes.ReconnectStateLost,
                    PlayerId = loginResult.UserId,
                    Account = account,
                    Message = string.IsNullOrWhiteSpace(resumeDecision.Reason)
                        ? "Server session state was lost. Start a new session instead of reconnecting."
                        : resumeDecision.Reason
                };
            }

            sessionKey = resumeDecision.Session.Value;
            await sessionGrain
                .ReconnectAsync(new PlayerSessionReconnectRequest
                {
                    UserId = loginResult.UserId,
                    SessionToken = loginResult.SessionToken,
                    ConnectionId = _connectionId,
                    ReconnectedAtUtc = DateTime.UtcNow,
                    ControlEdge = CloneEdge(_edgeNodeIdentity.RealtimeEndpoint)
                })
                .ConfigureAwait(false);
            await _reliableMatchmakingPublisher.ReplayPendingAsync(loginResult.UserId).ConfigureAwait(false);
        }
        else
        {
            sessionKey = await _sessionDirectory
                .RegisterNewControlAsync(loginResult.UserId, loginResult.SessionToken, _connectionId, _callback)
                .ConfigureAwait(false);
            await sessionGrain
                .AttachAsync(new PlayerSessionAttachRequest
            {
                UserId = loginResult.UserId,
                SessionToken = loginResult.SessionToken,
                ConnectionId = _connectionId,
                AttachedAtUtc = DateTime.UtcNow,
                ControlEdge = CloneEdge(_edgeNodeIdentity.RealtimeEndpoint)
            })
            .ConfigureAwait(false);
            await _reliablePushOutbox.AckAsync(loginResult.UserId, long.MaxValue).ConfigureAwait(false);
        }

        return new LoginReply
        {
            Code = LoginResultCodes.Ok,
            Token = loginResult.SessionToken,
            PlayerId = loginResult.UserId,
            WinCount = loginResult.WinCount,
            VictoryPoints = loginResult.VictoryPoints,
            Account = account,
            Password = req.GuestLogin ? password : string.Empty,
            SessionId = sessionKey.SessionId,
            SessionGeneration = sessionKey.Generation
        };
    }

    public async ValueTask<LeaderboardReply> GetLeaderboardAsync(LeaderboardRequest req)
    {
        ThrowIfDisposed();

        var topN = req.TopN <= 0 ? 10 : req.TopN;
        var snapshot = await _clusterClient.GetGrain<ILeaderboardGrain>(0)
            .GetLeaderboardAsync(topN)
            .ConfigureAwait(false);

        _logger.LogInformation("Leaderboard queried. TopN={TopN} Returned={Returned} Period={PeriodStartUtc}.",
            topN,
            snapshot.Entries.Count,
            snapshot.PeriodStartUtc);

        return new LeaderboardReply
        {
            Code = 0,
            PeriodStartUtc = snapshot.PeriodStartUtc,
            SecondsUntilReset = snapshot.SecondsUntilReset,
            Entries = snapshot.Entries.Select(static entry => new Shared.Interfaces.LeaderboardEntry
            {
                PlayerId = entry.PlayerId,
                VictoryPoints = entry.VictoryPoints,
                WinCount = entry.WinCount,
                Rank = entry.Rank
            }).ToList()
        };
    }

    public async ValueTask StartMatchmakingAsync(MatchmakingRequest req)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(_playerId))
        {
            return;
        }

        await _matchmakingCoordinator.EnqueueAsync(_playerId).ConfigureAwait(false);
    }

    public async ValueTask CancelMatchmakingAsync(CancelMatchmakingRequest req)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(_playerId))
        {
            return;
        }

        await _matchmakingCoordinator.CancelAsync(_playerId, "Matchmaking cancelled").ConfigureAwait(false);
    }

    public async ValueTask<RealtimeAttachReply> AttachRealtimeAsync(RealtimeAttachRequest req)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(req.PlayerId) ||
            string.IsNullOrWhiteSpace(req.Token) ||
            string.IsNullOrWhiteSpace(req.RoomId) ||
            string.IsNullOrWhiteSpace(req.MatchId))
        {
            return new RealtimeAttachReply
            {
                Code = 1,
                Message = "Realtime attach request is incomplete."
            };
        }

        var sessionSnapshot = await _clusterClient.GetGrain<IPlayerSessionGrain>(req.PlayerId)
            .GetSnapshotAsync()
            .ConfigureAwait(false);
        if (!string.Equals(sessionSnapshot.SessionToken, req.Token, StringComparison.Ordinal) ||
            !string.Equals(sessionSnapshot.CurrentRoomId, req.RoomId, StringComparison.Ordinal) ||
            !string.Equals(sessionSnapshot.CurrentMatchId, req.MatchId, StringComparison.Ordinal))
        {
            return new RealtimeAttachReply
            {
                Code = 2,
                Message = "Realtime session attach rejected."
            };
        }

        if (!_edgeNodeIdentity.IsRuntimeOwner(sessionSnapshot.RuntimeEdge))
        {
            return new RealtimeAttachReply
            {
                Code = 3,
                Message = "Realtime session must attach to the runtime owner edge."
            };
        }

        var room = await _clusterClient.GetGrain<Orleans.Contracts.Rooms.IRoomGrain>(req.RoomId)
            .GetSnapshotAsync()
            .ConfigureAwait(false);
        await _roomRuntimeHost.EnsureRoomReadyAsync(room).ConfigureAwait(false);

        _playerId = req.PlayerId;
        _connectionId = Guid.NewGuid().ToString("N");
        _isRealtimeConnection = true;

        var attached = await _sessionDirectory
            .AttachRealtimeAsync(req.PlayerId, req.Token, req.RoomId, req.MatchId, _connectionId, _callback)
            .ConfigureAwait(false);
        if (!attached)
        {
            _playerId = null;
            _connectionId = null;
            _isRealtimeConnection = false;
            return new RealtimeAttachReply
            {
                Code = 2,
                Message = "Realtime session attach rejected."
            };
        }

        return new RealtimeAttachReply
        {
            Code = 0,
            Message = "Realtime session attached.",
            PlayerId = req.PlayerId,
            RoomId = req.RoomId,
            MatchId = req.MatchId
        };
    }

    public async ValueTask<ReliablePushAckReply> AckReliablePushAsync(ReliablePushAckRequest req)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(_playerId) || req.Sequence <= 0)
        {
            return new ReliablePushAckReply
            {
                Code = ReliablePushAckResultCodes.InvalidRequest,
                Message = "Reliable push ack request is incomplete."
            };
        }

        if (!string.IsNullOrWhiteSpace(req.PlayerId) &&
            !string.Equals(req.PlayerId, _playerId, StringComparison.Ordinal))
        {
            return new ReliablePushAckReply
            {
                Code = ReliablePushAckResultCodes.InvalidRequest,
                Message = "Reliable push ack player does not match the current session."
            };
        }

        var registration = _sessionDirectory.Get(_playerId);
        if (registration is null)
        {
            return new ReliablePushAckReply
            {
                Code = ReliablePushAckResultCodes.SessionStateLost,
                RequiresNewSession = true,
                Message = "Server session state was lost. Start a new session instead of reconnecting."
            };
        }

        var currentSession = registration.SessionKey;
        var acknowledgedSession = string.IsNullOrWhiteSpace(req.SessionId) || req.SessionGeneration <= 0
            ? currentSession
            : new GameSessionKey(_playerId, req.SessionId, req.SessionGeneration);

        if (registration is not null &&
            !string.IsNullOrWhiteSpace(req.Token) &&
            !string.Equals(registration.SessionToken, req.Token, StringComparison.Ordinal))
        {
            return new ReliablePushAckReply
            {
                Code = ReliablePushAckResultCodes.InvalidRequest,
                Message = "Reliable push ack token does not match the current session."
            };
        }

        var outcome = await _reliablePushAckService
            .AckAsync(currentSession, acknowledgedSession, req.Sequence)
            .ConfigureAwait(false);

        if (outcome.Status == ReliablePushAckStatus.StateLost)
        {
            return new ReliablePushAckReply
            {
                Code = ReliablePushAckResultCodes.SessionStateLost,
                RequiresNewSession = true,
                Message = "Client acknowledged a reliable push sequence unknown to the server."
            };
        }

        if (outcome.Status == ReliablePushAckStatus.SessionMismatch)
        {
            return new ReliablePushAckReply
            {
                Code = ReliablePushAckResultCodes.SessionStateLost,
                RequiresNewSession = true,
                Message = "Reliable push ack belongs to a different session generation."
            };
        }

        return new ReliablePushAckReply { Code = ReliablePushAckResultCodes.Ok };
    }

    public async ValueTask SubmitInput(InputMessage req)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(_playerId))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(req.PlayerId) &&
            !string.Equals(req.PlayerId, _playerId, StringComparison.Ordinal))
        {
            return;
        }

        var sessionSnapshot = await _clusterClient.GetGrain<IPlayerSessionGrain>(_playerId)
            .GetSnapshotAsync()
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(sessionSnapshot.CurrentRoomId) ||
            !_edgeNodeIdentity.IsRuntimeOwner(sessionSnapshot.RuntimeEdge))
        {
            return;
        }

        req.PlayerId = _playerId;
        await _roomRuntimeHost.SubmitInputAsync(sessionSnapshot.CurrentRoomId, _playerId, req).ConfigureAwait(false);
    }

    public async ValueTask LogoutAsync(LogoutRequest req)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(_playerId))
        {
            return;
        }

        await ReleasePlayerAsync(_playerId, "Logout").ConfigureAwait(false);
        _playerId = null;
        _connectionId = null;
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (!string.IsNullOrWhiteSpace(_playerId))
        {
            if (_isRealtimeConnection && !_controlLoggedIn)
            {
                await ReleaseRealtimeAsync(_playerId, "Realtime disconnect").ConfigureAwait(false);
            }
            else if (_controlLoggedIn)
            {
                await MarkControlDisconnectedAsync(_playerId, "Control disconnect").ConfigureAwait(false);
            }
            else
            {
                await ReleasePlayerAsync(_playerId, "Dispose").ConfigureAwait(false);
            }

            _playerId = null;
            _connectionId = null;
        }
    }

    private async Task ReleasePlayerAsync(string playerId, string reason)
    {
        var registration = _sessionDirectory.Get(playerId);
        try
        {
            await _matchmakingCoordinator.ReleasePlayerAsync(playerId, reason).ConfigureAwait(false);
            await _clusterClient.GetGrain<IPlayerSessionGrain>(playerId)
                .MarkDisconnectedAsync(new PlayerSessionDisconnectRequest
                {
                    UserId = playerId,
                    ConnectionId = registration?.ConnectionId ?? string.Empty,
                    DisconnectedAtUtc = DateTime.UtcNow,
                    Reason = reason
                })
                .ConfigureAwait(false);
            await _clusterClient.GetGrain<IUserGrain>(playerId)
                .SetOnlineAsync(false)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release player {PlayerId} during {Reason}.", playerId, reason);
        }

        if (registration is not null && !string.IsNullOrWhiteSpace(registration.RoomId))
        {
            _sessionDirectory.ClearRoom(playerId, registration.RoomId);
        }

        _sessionDirectory.Remove(playerId);
    }

    private async Task MarkControlDisconnectedAsync(string playerId, string reason)
    {
        var connectionId = _connectionId ?? string.Empty;
        var disconnectedAtUtc = DateTime.UtcNow;
        try
        {
            await _clusterClient.GetGrain<IPlayerSessionGrain>(playerId)
                .MarkDisconnectedAsync(new PlayerSessionDisconnectRequest
                {
                    UserId = playerId,
                    ConnectionId = connectionId,
                    DisconnectedAtUtc = disconnectedAtUtc,
                    Reason = reason
                })
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark control disconnect for player {PlayerId} during {Reason}.", playerId, reason);
        }

        await _sessionDirectory.MarkControlDisconnectedAsync(playerId, connectionId, disconnectedAtUtc).ConfigureAwait(false);
    }

    private Task ReleaseRealtimeAsync(string playerId, string reason)
    {
        return _sessionDirectory.DetachRealtimeAsync(playerId, _connectionId).AsTask();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static EdgeEndpointDescriptor CloneEdge(EdgeEndpointDescriptor edge)
    {
        return new EdgeEndpointDescriptor
        {
            InstanceId = edge.InstanceId,
            Transport = edge.Transport,
            Host = edge.Host,
            Port = edge.Port,
            Path = edge.Path
        };
    }

    private static string CreateGuestAccount()
    {
        return $"guest-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{RandomNumberGenerator.GetHexString(6).ToLowerInvariant()}";
    }

    private static string CreateGuestPassword()
    {
        return RandomNumberGenerator.GetHexString(16).ToLowerInvariant();
    }
}
