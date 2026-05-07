#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Agar.Godot.Scripts.Networking;
using Godot;
using Shared.Interfaces;
using ULinkGame.Client.ReliablePush;
using ULinkRPC.Client;

namespace Agar.Godot.Scripts;

public partial class Main : Node2D
{
    private readonly object _gate = new();
    private readonly ReliablePushTracker _pushTracker = new();
    private readonly CallbackReceiver _callbackReceiver;
    private AgarNetworkSession? _session;
    private CancellationTokenSource? _cts;
    private WorldState _worldState = new();
    private string _status = "Starting";
    private string _localPlayerId = string.Empty;
    private double _nextInputAt;
    private bool _inputInFlight;
    private bool _realtimeConnectInFlight;

    [Export]
    public string Host { get; set; } = "127.0.0.1";

    [Export]
    public int ControlPort { get; set; } = 20000;

    [Export]
    public string ControlPath { get; set; } = "/ws";

    [Export]
    public float InputRateHz { get; set; } = 30f;

    public Main()
    {
        _callbackReceiver = new CallbackReceiver(this);
    }

    public override void _Ready()
    {
        _cts = new CancellationTokenSource();
        _session = new AgarNetworkSession(OnDisconnected);
        _ = ConnectAndStartAsync(_cts.Token);
    }

    public override void _Process(double delta)
    {
        if (_session is { CanSubmitGameplayInput: true } &&
            !string.IsNullOrWhiteSpace(_localPlayerId) &&
            Time.GetTicksMsec() / 1000.0 >= _nextInputAt)
        {
            _nextInputAt = Time.GetTicksMsec() / 1000.0 + 1.0 / Math.Max(1f, InputRateHz);
            SubmitCurrentInput();
        }

        QueueRedraw();
    }

    public override void _ExitTree()
    {
        _cts?.Cancel();
        if (_session != null)
        {
            _ = _session.DisposeAsync();
        }
    }

    public override void _Draw()
    {
        WorldState worldState;
        string status;
        string localPlayerId;
        long reliableSequence;

        lock (_gate)
        {
            worldState = _worldState;
            status = _status;
            localPlayerId = _localPlayerId;
            reliableSequence = _pushTracker.LastAppliedSequence;
        }

        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(24, 32),
            $"Agar.Godot online | {status} | player {localPlayerId} | tick {worldState.Tick} | reliable seq {reliableSequence}");

        var center = GetViewportRect().Size * 0.5f;
        foreach (var pickup in worldState.Pickups)
        {
            DrawCircle(center + new Vector2(pickup.X, pickup.Y) * 6f, 2.5f, new Color(0.42f, 0.85f, 0.52f));
        }

        foreach (var player in worldState.Players)
        {
            var color = player.PlayerId == localPlayerId
                ? new Color(0.2f, 0.62f, 1f)
                : new Color(1f, 0.56f, 0.24f);

            DrawCircle(center + new Vector2(player.X, player.Y) * 6f, Mathf.Max(6f, player.Radius * 6f), color);
        }
    }

    private async Task ConnectAndStartAsync(CancellationToken cancellationToken)
    {
        try
        {
            SetStatus($"Connecting {Host}:{ControlPort}{ControlPath}");
            var session = _session ?? throw new InvalidOperationException("Network session is not initialized.");
            var login = await session
                .ConnectAndLoginAsync(
                    Host,
                    ControlPort,
                    ControlPath,
                    $"godot-{Guid.NewGuid():N}",
                    string.Empty,
                    _callbackReceiver,
                    cancellationToken)
                .ConfigureAwait(false);

            if (login.Code != LoginResultCodes.Ok)
            {
                SetStatus($"Login failed: {login.Message}");
                return;
            }

            lock (_gate)
            {
                _localPlayerId = login.PlayerId;
                _status = "Logged in, matchmaking";
            }

            await session.StartMatchmakingAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetStatus($"Connection failed: {ex.Message}");
            GD.PushError(ex.ToString());
        }
    }

    private void SubmitCurrentInput()
    {
        if (_inputInFlight || _session == null)
        {
            return;
        }

        var direction = ReadMoveDirection();
        string playerId;
        int tick;

        lock (_gate)
        {
            playerId = _localPlayerId;
            tick = _worldState.Tick;
        }

        _inputInFlight = true;
        _ = SubmitInputAsync(new InputMessage
        {
            PlayerId = playerId,
            MoveX = direction.X,
            MoveY = direction.Y,
            Tick = tick
        });
    }

    private async Task SubmitInputAsync(InputMessage input)
    {
        try
        {
            if (_session != null)
            {
                await _session.SubmitInputAsync(input).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Input submit failed: {ex.Message}");
        }
        finally
        {
            _inputInFlight = false;
        }
    }

    private void ApplyWorldState(WorldState worldState)
    {
        lock (_gate)
        {
            _worldState = worldState;
            _status = _session is { IsRealtimeConnected: true } ? "In match (realtime)" : "In match";
        }
    }

    private void ApplyMatchmakingStatus(MatchmakingStatusUpdate status)
    {
        var decision = _pushTracker.Decide(status.ReliableSequence);
        if (!decision.ShouldApply)
        {
            if (decision.ShouldAck)
            {
                _ = AckReliablePushAsync(decision.Sequence);
            }

            return;
        }

        lock (_gate)
        {
            _status = status.State switch
            {
                MatchmakingState.Queued => $"Queued {status.QueuePosition}/{status.QueueSize}",
                MatchmakingState.Searching => $"Searching {status.MatchedPlayerCount}/{status.RoomCapacity}",
                MatchmakingState.Matched => "Matched, connecting realtime",
                MatchmakingState.Canceled => "Matchmaking canceled",
                MatchmakingState.Failed => $"Matchmaking failed: {status.Message}",
                _ => status.Message
            };
        }

        if (status.State == MatchmakingState.Matched &&
            status.RealtimeConnection is { Transport: RealtimeTransportKind.Kcp } realtimeConnection)
        {
            _ = EnsureRealtimeAsync(CloneRealtimeConnection(realtimeConnection));
        }

        if (decision.ShouldAck)
        {
            _pushTracker.MarkApplied(decision.Sequence);
            _ = AckReliablePushAsync(decision.Sequence);
        }
    }

    private async Task EnsureRealtimeAsync(RealtimeConnectionInfo realtimeConnection)
    {
        if (_realtimeConnectInFlight || _session == null || _cts == null)
        {
            return;
        }

        _realtimeConnectInFlight = true;
        try
        {
            var connected = await _session
                .EnsureRealtimeConnectedAsync(realtimeConnection, _callbackReceiver, _cts.Token)
                .ConfigureAwait(false);

            SetStatus(connected ? "Realtime connected" : "Realtime attach failed, using control channel");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetStatus($"Realtime connect failed: {ex.Message}");
            GD.PushError(ex.ToString());
        }
        finally
        {
            _realtimeConnectInFlight = false;
        }
    }

    private async Task AckReliablePushAsync(long sequence)
    {
        try
        {
            if (_session != null && _cts != null)
            {
                await _session.AckReliablePushAsync(sequence, _cts.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Reliable push ack failed: {ex.Message}");
        }
    }

    private void OnDisconnected(Exception? ex)
    {
        SetStatus(ex == null ? "Disconnected" : $"Disconnected: {ex.Message}");
    }

    private void SetStatus(string status)
    {
        lock (_gate)
        {
            _status = status;
        }
    }

    private static RealtimeConnectionInfo CloneRealtimeConnection(RealtimeConnectionInfo source)
    {
        return new RealtimeConnectionInfo
        {
            Transport = source.Transport,
            Host = source.Host,
            Port = source.Port,
            Path = source.Path,
            RoomId = source.RoomId,
            MatchId = source.MatchId,
            SessionToken = source.SessionToken
        };
    }

    private static Vector2 ReadMoveDirection()
    {
        var direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");

        if (Input.IsKeyPressed(Key.A))
        {
            direction.X -= 1f;
        }

        if (Input.IsKeyPressed(Key.D))
        {
            direction.X += 1f;
        }

        if (Input.IsKeyPressed(Key.W))
        {
            direction.Y -= 1f;
        }

        if (Input.IsKeyPressed(Key.S))
        {
            direction.Y += 1f;
        }

        return direction.LengthSquared() > 1f ? direction.Normalized() : direction;
    }

    private sealed class CallbackReceiver : RpcClient.PlayerCallbackBase
    {
        private readonly Main _owner;

        public CallbackReceiver(Main owner)
        {
            _owner = owner;
        }

        public override void OnWorldState(WorldState worldState)
        {
            _owner.ApplyWorldState(worldState);
        }

        public override void OnPlayerDead(PlayerDead deadEvent)
        {
            _owner.SetStatus(deadEvent.PlayerId == _owner._localPlayerId ? "You were eaten" : $"{deadEvent.PlayerId} was eaten");
        }

        public override void OnMatchEnd(MatchEnd matchEnd)
        {
            _owner.SetStatus($"Match end, winner {matchEnd.WinnerPlayerId}");
        }

        public override void OnMatchmakingStatus(MatchmakingStatusUpdate matchmakingStatus)
        {
            _owner.ApplyMatchmakingStatus(matchmakingStatus);
        }
    }
}
