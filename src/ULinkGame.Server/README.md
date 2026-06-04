# ULinkGame.Server

`ULinkGame.Server` provides .NET server hosting helpers for ULinkRPC, ULinkActor-based game-state execution, session lifecycle, endpoint callback binding, and reliable business push.

It builds on ULinkActor so room, battle, and service state can run with predictable process-local mailbox behavior on the gateway process.

## Install

```powershell
dotnet add package ULinkGame.Server
```

## Runtime Guardrails

ULinkGame.Server provides runtime guardrail diagnostics for framework invariants such as node identity, endpoint shape, hotfix assembly presence, and cluster service graph consistency. Generated projects use these diagnostics through `--ulinkgame-check`; server hosts can also register the default rules with `AddULinkGameRuntimeValidation()`.

## Host ULinkRPC servers

Register one or more named RPC server configurators in your gateway process:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ULinkGame.Server;
using ULinkGame.Server.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<PlayerService>();
builder.Services.AddULinkGameServer();
builder.Services.AddULinkRpcServer<ControlPlaneRpcServerConfigurator>();
builder.Services.AddULinkGameServerGateway();

await builder.Build().RunAsync();
```

Implement `IULinkRpcServerConfigurator` to choose the serializer, transport, and generated service binder:

```csharp
using Microsoft.Extensions.DependencyInjection;
using ULinkGame.Server.Hosting;
using ULinkRPC.Serializer.MemoryPack;
using ULinkRPC.Transport.WebSocket;

public sealed class ControlPlaneRpcServerConfigurator : IULinkRpcServerConfigurator
{
    public string Name => "control";

    public void Configure(ULinkGameServerRpcContext context)
    {
        context.Builder
            .UseSerializer(new MemoryPackRpcSerializer())
            .UseAcceptor(async ct => await WsConnectionAcceptor.CreateAsync(20000, "/ws", ct));

        PlayerServiceBinder.Bind(
            context.Builder.ServiceRegistry,
            callback => ActivatorUtilities.CreateInstance<PlayerService>(context.Services, callback));
    }
}
```

`Name` only identifies the hosted RPC server inside the process. Register another configurator if you need another endpoint.

## Use Actors

ULinkGame's server-side actor execution model is built on the standalone `ULinkActor` runtime through a process-local facade. Register the ULinkGame server services in the same .NET host that runs your gateway:

```csharp
using ULinkGame.Server;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddULinkGameServer();
```

Messages for the same actor are processed through one mailbox, so actor state can be written without locks:

```csharp
using ULinkGame.Server.Actors;

public sealed class RoomActor : Actor
{
    private int _joinedPlayers;

    public int JoinedPlayers => _joinedPlayers;

    public ValueTask JoinAsync(long playerId, CancellationToken cancellationToken)
    {
        _joinedPlayers++;
        return ValueTask.CompletedTask;
    }
}

var provider = builder.Build().Services;
var runtime = provider.GetRequiredService<IActorRuntime>();
await runtime.TellAsync<RoomActor>(
    ActorId.From("room/alpha"),
    static (room, ct) => room.JoinAsync(10001, ct));
```

Use `AskAsync` when the caller needs a response:

```csharp
var count = await runtime.AskAsync<RoomActor, int>(
    ActorId.From("room/alpha"),
    static (room, _) => ValueTask.FromResult(room.JoinedPlayers));
```

Use `TryTell` when a caller must fail fast instead of waiting for mailbox capacity:

```csharp
var result = runtime.TryTell<RoomActor>(
    ActorId.From("room/alpha"),
    static (room, ct) => room.JoinAsync(10002, ct));
```

`ActorTellResult.MailboxFull` means the actor's local mailbox rejected the immediate send. Callers can retry later, shed work, or map the result to cluster backpressure.

The facade also supports explicit actor stop/drain, mailbox metrics, mailbox-delivered timers, and lifecycle hooks:

- `StopAsync(id)` removes an actor after draining its mailbox.
- `StopAsync(id, drainTimeout)` returns `ActorStopOutcome.TimedOut` when the actor cannot drain in time.
- `TryGetMailboxMetrics(id, out metrics)` returns ULinkGame-owned mailbox metrics without exposing `ULinkActor` runtime types.
- `Actor.Context.RegisterTimer(...)` and `IActorRuntime.RegisterTimer(...)` schedule timer ticks through the actor mailbox.
- Override `OnActivateAsync` and `OnDeactivateAsync` for explicit startup and stop cleanup.

ULinkActor is the foundation for actor/mailbox runtime concerns. ULinkGame.Server builds on it through its facade and keeps the game-session layer focused on session identity, endpoint binding, reconnect, and reliable push integration. Add `ULinkActor.SourceGenerator` directly only when your game project chooses native ULinkActor typed actors or generated actor clients.

`ClusterActorDispatcher<TActor>` can adapt an explicit `ULinkGame.Cluster` actor envelope into the local `IActorRuntime` mailbox and wait for a handler result. `ClusterActorTellDispatcher<TActor>` is the one-way variant that uses `TryTell` and maps local mailbox pressure to `ClusterSendStatus.Backpressure`. Both adapters are intentionally typed and require application-provided handler delegates; they do not expose transparent remote actor references or generated remote actor proxies.

## Main Server API

Register the recommended runtime services with one call:

```csharp
using ULinkGame.Server;

builder.Services.AddULinkGameServer();
```

Use `IULinkGameServer` as the main entry point for sessions, endpoint callback bindings, and reliable push:

```csharp
using ULinkGame.Abstractions;
using ULinkGame.Server;

public sealed class MatchPushService
{
    private readonly IULinkGameServer _server;

    public MatchPushService(IULinkGameServer server)
    {
        _server = server;
    }

    public ValueTask<GameSessionKey> LoginAsync(
        string playerId,
        string connectionId,
        IPlayerCallback callback,
        CancellationToken ct)
    {
        return _server.StartSessionAsync(playerId, GameEndpointName.Control, connectionId, callback, ct);
    }

    public ValueTask<long> PublishMatchedAsync(
        GameSessionKey session,
        MatchmakingStatusUpdate payload,
        CancellationToken ct)
    {
        return _server.PublishReliablePushAsync<IPlayerCallback, MatchmakingStatusUpdate>(
            session,
            GameEndpointName.Control,
            "matched",
            payload,
            static (callback, sequence, update, _) =>
            {
                update.ReliableSequence = sequence.Value;
                return callback.OnMatchmakingStatus(update);
            },
            ct);
    }

    public ValueTask ReplayAsync(GameSessionKey session, CancellationToken ct)
    {
        return _server.ReplayReliablePushAsync<IPlayerCallback, MatchmakingStatusUpdate>(
            session,
            GameEndpointName.Control,
            "matched",
            static (callback, sequence, update, _) =>
            {
                update.ReliableSequence = sequence.Value;
                return callback.OnMatchmakingStatus(update);
            },
            ct);
    }

    public ValueTask<ReliablePushAckOutcome> AckAsync(
        GameSessionKey currentSession,
        GameSessionKey acknowledgedSession,
        long sequence,
        CancellationToken ct)
    {
        return _server.AckReliablePushAsync(currentSession, acknowledgedSession, sequence, ct);
    }

}
```

The built-in outbox is process-local and in-memory. Replace `IReliablePushOutbox` with a project-specific implementation when pending pushes must survive process restarts. Use `IGameSessionDirectory`, `IReliablePushOutbox`, `ReliablePushRecord`, and `IReliablePushAckService` directly only when you need lower-level control.

## Use Session Lifecycle Helpers

The main API already registers in-memory session helpers. If you need only the lower-level session services, register them directly:

```csharp
using ULinkGame.Abstractions;
using ULinkGame.Server.Sessions;

builder.Services.AddULinkGameServerSessions();
```

`IGameSessionDirectory` stores session identity, endpoint bindings, and opaque typed callbacks. Endpoint names are application data, so `"control"` and `"realtime"` are sample conventions rather than framework requirements.

For reconnect, use `IGameSessionResumeService` so token validation and authoritative state checks stay in one place:

```csharp
using ULinkGame.Server.Sessions;
using ULinkGame.Abstractions;

public sealed class PlayerLoginService
{
    private readonly IGameSessionResumeService _resume;

    public PlayerLoginService(IGameSessionResumeService resume)
    {
        _resume = resume;
    }

    public async ValueTask<SessionResumeDecision> ResumeAsync(GameSessionKey session, string token, CancellationToken ct)
    {
        var decision = await _resume.TryResumeAsync(new GameSessionResumeRequest(session, token), ct);

        return decision.Status switch
        {
            SessionResumeStatus.Resumed => decision,
            SessionResumeStatus.StateRefreshRequired => decision,
            SessionResumeStatus.StateLost => decision,
            SessionResumeStatus.Unauthorized => decision,
            _ => decision
        };
    }
}
```

Projects can register `IGameSessionTokenValidator` and `IAuthoritativeSessionStateProbe` to decide whether a reconnect is accepted, requires a snapshot refresh, or must start a new session. ULinkGame does not define account models, token formats, room snapshots, or gameplay DTOs.

## Feature/Role Assembly

Compose servers from declarative features and deploy with role-based filtering. Develop with everything in one process, split into multiple processes in production — without code changes.

```csharp
// Define a role
public sealed class GatewayRole : INodeRole
{
    public string Name => "gateway";
    public IFeature[] Features => [new ClusterFeature(), new AuthFeature()];
}

// Configure in Program.cs
builder.Services.AddFeatures(builder.Configuration, features =>
{
    features.FromAssembly(typeof(GatewayRole).Assembly);
});
```

```bash
dotnet run                                     # all roles
dotnet run --ULinkGame:Features:Roles=gateway   # gateway only
```

See `docs/feature-role.md` for details.

## Remote Actor Messaging

Use generated typed actor accessors for frequent actor calls. Local and remote calls expose the same business methods, while `Remote(nodeId, id)` keeps the network boundary visible:

```csharp
var localReply = await rooms
    .Local(roomId)
    .JoinAsync(request, cancellationToken);

var remoteReply = await rooms
    .Remote(nodeId, roomId)
    .JoinAsync(request, cancellationToken);
```

The lower-level `AskRemoteAsync` and `TellRemoteAsync` helpers remain available for custom cluster actor envelope plumbing.

See `docs/remote-actor-messaging.md` for details.

## Message Recording

Capture every actor message dispatch for offline debugging. One line to enable:

```csharp
builder.Services.AddMessageRecording();

// Later
var store = provider.GetRequiredService<IMessageLogStore>();
var log = await store.GetLogAsync(ActorId.From("player/alice"));
```

See `docs/message-recording.md` for details.

## Actor Runtime Configuration

```csharp
builder.Services.AddULinkGameServerActors(options =>
{
    options.MailboxCapacity = 4096;
    options.SlowMessageThreshold = TimeSpan.FromSeconds(1);
    options.CallTimeout = TimeSpan.FromSeconds(30);
});

// Query actor lifecycle state
var state = runtime.GetState(actorId);  // Active / Draining / Dead
```
