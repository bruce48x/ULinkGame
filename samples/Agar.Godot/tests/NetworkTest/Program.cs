using System.Diagnostics;
using Rpc.Generated;
using Shared.Interfaces;
using ULinkRPC.Client;
using ULinkRPC.Core;
using ULinkRPC.Serializer.MemoryPack;
using ULinkRPC.Transport.Kcp;
using ULinkRPC.Transport.WebSocket;

var gatewayProcess = await StartGatewayAsync();
try
{
    await RunNetworkTestAsync();
    Console.WriteLine("[TEST] Network test PASSED.");
}
catch (Exception ex)
{
    Console.WriteLine($"[TEST] FAILED: {ex}");
    Environment.ExitCode = 1;
}
finally
{
    StopGateway(gatewayProcess);
}

static async Task<Process> StartGatewayAsync()
{
    var psi = new ProcessStartInfo("dotnet", "run --project samples/Agar.Godot/Server/Gateway/Gateway.csproj --no-build --no-restore")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        WorkingDirectory = Environment.CurrentDirectory
    };
    psi.Environment["DOTNET_ENVIRONMENT"] = "Development";

    var process = new Process { StartInfo = psi };
    var readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    process.OutputDataReceived += (_, e) =>
    {
        if (e.Data == null) return;
        Console.WriteLine($"[GW] {e.Data}");
        if (e.Data.Contains("listening on ws://", StringComparison.Ordinal))
            readyTcs.TrySetResult(true);
    };
    process.ErrorDataReceived += (_, e) =>
    {
        if (e.Data != null)
        {
            Console.Error.WriteLine($"[GW-ERR] {e.Data}");
            if (e.Data.Contains("listening on ws://", StringComparison.Ordinal))
                readyTcs.TrySetResult(true);
        }
    };

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    Console.WriteLine($"[TEST] Started Gateway (PID={process.Id}), waiting for listen...");

    if (!readyTcs.Task.Wait(TimeSpan.FromSeconds(30)))
    {
        process.Kill(entireProcessTree: true);
        throw new TimeoutException("Gateway did not start listening within 30 seconds.");
    }

    Console.WriteLine("[TEST] Gateway is listening.");
    await Task.Delay(1000);
    return process;
}

static void StopGateway(Process process)
{
    if (process.HasExited) return;
    Console.WriteLine("[TEST] Stopping Gateway...");
    process.Kill(entireProcessTree: true);
    process.WaitForExit(10000);
    Console.WriteLine("[TEST] Gateway stopped.");
}

static async Task RunNetworkTestAsync()
{
    var receivedWorldStates = new List<WorldState>();
    var matchmakingTcs = new TaskCompletionSource<RealtimeConnectionInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
    var disconnectedTcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
    var controlAlive = false;

    var testCallbacks = new TestCallbacks(
        onWorldState: ws => { lock (receivedWorldStates) receivedWorldStates.Add(ws); },
        onMatchmakingStatus: status =>
        {
            Console.WriteLine($"[TEST] Matchmaking: State={status.State}, Queue={status.QueuePosition}/{status.QueueSize}, Msg={status.Message}");
            if (status.State == MatchmakingState.Matched && status.RealtimeConnection != null)
            {
                matchmakingTcs.TrySetResult(status.RealtimeConnection);
            }
            else if (status.State == MatchmakingState.Failed)
            {
                matchmakingTcs.TrySetException(new Exception($"Matchmaking failed: {status.Message}"));
            }
        });

    var callbacks = new RpcClient.RpcNotificationBindings();
    callbacks.Add(testCallbacks);

    var controlClient = new RpcClient(
        new RpcClientOptions(
            new WsTransport("ws://127.0.0.1:20000/ws"),
            new MemoryPackRpcSerializer())
        {
            KeepAlive = new RpcKeepAliveOptions
            {
                Enabled = true,
                Interval = TimeSpan.FromSeconds(5),
                Timeout = TimeSpan.FromSeconds(15)
            }
        },
        callbacks);

    controlClient.Disconnected += ex =>
    {
        controlAlive = false;
        disconnectedTcs.TrySetResult(ex?.Message);
    };

    RpcClient? realtimeClient = null;

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

    try
    {
        await controlClient.ConnectAsync(cts.Token);
        controlAlive = true;
        Console.WriteLine("[TEST] Control WebSocket connected.");

        var playerService = controlClient.Api.Shared.Player;
        var login = await playerService.LoginAsync(new LoginRequest
        {
            Account = "headless-test",
            Password = "test",
            GuestLogin = true
        });
        Console.WriteLine($"[TEST] Login: Code={login.Code}, PlayerId={login.PlayerId}");

        if (login.Code != LoginResultCodes.Ok)
            throw new Exception($"Login failed: {login.Message}");

        await playerService.StartMatchmakingAsync(new MatchmakingRequest
        {
            PlayerId = login.PlayerId,
            Token = login.Token
        });
        Console.WriteLine("[TEST] Matchmaking started.");

        Console.WriteLine("[TEST] Waiting for match (up to 20s)...");
        var realtimeInfo = await matchmakingTcs.Task.WaitAsync(TimeSpan.FromSeconds(20), cts.Token);
        Console.WriteLine($"[TEST] Matched! Room={realtimeInfo.RoomId}, Host={realtimeInfo.Host}:{realtimeInfo.Port}");

        realtimeClient = new RpcClient(
            new RpcClientOptions(
                new KcpTransport(realtimeInfo.Host, realtimeInfo.Port),
                new MemoryPackRpcSerializer())
            {
                KeepAlive = new RpcKeepAliveOptions
                {
                    Enabled = true,
                    Interval = TimeSpan.FromSeconds(2),
                    Timeout = TimeSpan.FromSeconds(6)
                }
            },
            callbacks);

        await realtimeClient.ConnectAsync(cts.Token);
        Console.WriteLine("[TEST] Realtime KCP connected.");

        var realtimePlayerService = realtimeClient.Api.Shared.Player;
        var attach = await realtimePlayerService.AttachRealtimeAsync(new RealtimeAttachRequest
        {
            PlayerId = login.PlayerId,
            Token = string.IsNullOrWhiteSpace(realtimeInfo.SessionToken) ? login.Token : realtimeInfo.SessionToken,
            RoomId = realtimeInfo.RoomId ?? "",
            MatchId = realtimeInfo.MatchId ?? ""
        });
        Console.WriteLine($"[TEST] Realtime attach: Code={attach.Code}, Msg={attach.Message}");

        if (attach.Code != 0)
            throw new Exception($"Realtime attach failed: {attach.Message}");

        Console.WriteLine("[TEST] Submitting inputs...");
        for (int i = 0; i < 10; i++)
        {
            await realtimePlayerService.SubmitInput(new InputMessage
            {
                PlayerId = login.PlayerId,
                MoveX = 0.5f,
                MoveY = 0.3f,
                Tick = i
            });
            await Task.Delay(200, cts.Token);
        }

        await Task.Delay(2000, cts.Token);

        await realtimePlayerService.LogoutAsync(new LogoutRequest());
        Console.WriteLine("[TEST] Logged out.");
    }
    finally
    {
        if (controlAlive)
        {
            try { await controlClient.DisposeAsync(); } catch { }
        }
        if (realtimeClient != null)
        {
            try { await realtimeClient.DisposeAsync(); } catch { }
        }
    }

    lock (receivedWorldStates)
    {
        Console.WriteLine($"[TEST] Received {receivedWorldStates.Count} world state snapshots.");
        if (receivedWorldStates.Count > 0)
        {
            var ws = receivedWorldStates[^1];
            Console.WriteLine($"[TEST] Last world state: Tick={ws.Tick}, Players={ws.Players.Count}, Pickups={ws.Pickups.Count}");
        }
    }

    if (receivedWorldStates.Count == 0)
        throw new Exception("No world state snapshots received.");
}

internal sealed class TestCallbacks : RpcClient.PlayerCallbackBase
{
    private readonly Action<WorldState>? _onWorldState;
    private readonly Action<MatchmakingStatusUpdate>? _onMatchmakingStatus;

    public TestCallbacks(
        Action<WorldState>? onWorldState = null,
        Action<MatchmakingStatusUpdate>? onMatchmakingStatus = null)
    {
        _onWorldState = onWorldState;
        _onMatchmakingStatus = onMatchmakingStatus;
    }

    public override void OnWorldState(WorldState worldState)
    {
        _onWorldState?.Invoke(worldState);
    }

    public override void OnPlayerDead(PlayerDead deadEvent)
    {
        Console.WriteLine($"[TEST] PlayerDead: {deadEvent.PlayerId} tick={deadEvent.Tick}");
    }

    public override void OnMatchEnd(MatchEnd matchEnd)
    {
        Console.WriteLine($"[TEST] MatchEnd: winner={matchEnd.WinnerPlayerId}");
    }

    public override void OnMatchmakingStatus(MatchmakingStatusUpdate matchmakingStatus)
    {
        _onMatchmakingStatus?.Invoke(matchmakingStatus);
    }
}
