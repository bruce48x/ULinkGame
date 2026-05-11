# ULinkGame.Server

`ULinkGame.Server` provides .NET server hosting helpers for ULinkRPC, Microsoft Orleans, and reliable business push.

## Install

```powershell
dotnet add package ULinkGame.Server
```

## Host ULinkRPC servers

Register one or more named RPC server configurators in your gateway process:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ULinkGame.Server.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<PlayerService>();
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

## Connect to Orleans

For a gateway or API process that needs an Orleans client:

```csharp
using ULinkGame.Server.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.AddULinkGameServerOrleansClient();
```

For a silo process:

```csharp
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;
using ULinkGame.Server.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .UseULinkGameServerOrleansSilo((context, silo) =>
    {
        silo.AddMemoryGrainStorage("sessions");
    })
    .Build();

await host.RunAsync();
```

The helper reads these optional configuration keys:

- `Orleans:ClusterId`
- `Orleans:ServiceId`
- `Orleans:SiloPort`
- `Orleans:GatewayPort`
- `Orleans:AdvertisedIPAddress`

The default clustering setup is for local development. Configure Orleans storage and production clustering in your project.

## Use Reliable Push

Register the in-memory reliable push outbox:

```csharp
using ULinkGame.Server.ReliablePush;

builder.Services.AddULinkGameServerReliablePush(options =>
{
    options.MaxPendingPerOwner = 128;
});
```

Publish, replay, and acknowledge records from your own RPC service. The low-level API accepts an owner key:

```csharp
using ULinkGame.Server.ReliablePush;

public sealed class MatchPushService
{
    private readonly IReliablePushOutbox _outbox;

    public MatchPushService(IReliablePushOutbox outbox)
    {
        _outbox = outbox;
    }

    public ValueTask<long> PublishMatchedAsync(string playerId, object payload, CancellationToken ct)
    {
        return _outbox.PublishAsync(playerId, "matched", payload, DeliverAsync, ct);
    }

    public ValueTask ReplayAsync(string playerId, CancellationToken ct)
    {
        return _outbox.ReplayPendingAsync(playerId, DeliverAsync, ct);
    }

    public ValueTask AckAsync(string playerId, long sequence, CancellationToken ct)
    {
        return _outbox.AckAsync(playerId, sequence, ct);
    }

    private static ValueTask DeliverAsync(ReliablePushRecord record)
    {
        // Send record.Payload and record.Sequence through your ULinkRPC callback.
        return ValueTask.CompletedTask;
    }
}
```

When your game has a logical session id and generation, prefer the session-scoped overloads. They prevent an old session acknowledgement from pruning records for a newer session owned by the same player.

```csharp
using ULinkGame.Server.ReliablePush;
using ULinkGame.Server.Sessions;

public sealed class MatchPushService
{
    private readonly IReliablePushOutbox _outbox;
    private readonly IReliablePushAckService _acks;

    public MatchPushService(IReliablePushOutbox outbox, IReliablePushAckService acks)
    {
        _outbox = outbox;
        _acks = acks;
    }

    public ValueTask<long> PublishMatchedAsync(GameSessionKey session, object payload, CancellationToken ct)
    {
        return _outbox.PublishAsync(session, "matched", payload, DeliverAsync, ct);
    }

    public ValueTask ReplayAsync(GameSessionKey session, CancellationToken ct)
    {
        return _outbox.ReplayPendingAsync(session, DeliverAsync, ct);
    }

    public ValueTask<ReliablePushAckOutcome> AckAsync(
        GameSessionKey currentSession,
        GameSessionKey acknowledgedSession,
        long sequence,
        CancellationToken ct)
    {
        return _acks.AckAsync(currentSession, acknowledgedSession, sequence, ct);
    }

    private static ValueTask DeliverAsync(ReliablePushRecord record)
    {
        return ValueTask.CompletedTask;
    }
}
```

The built-in outbox is process-local and in-memory. Replace `IReliablePushOutbox` with a project-specific implementation when pending pushes must survive process restarts.

## Use Session Lifecycle Helpers

Register in-memory session helpers:

```csharp
using ULinkGame.Server.Sessions;

builder.Services.AddULinkGameServerSessions();
```

`IGameSessionDirectory` stores session identity, endpoint bindings, and opaque typed callbacks. Endpoint names are application data, so `"control"` and `"realtime"` are sample conventions rather than framework requirements.

For reconnect, use `IGameSessionResumeService` so token validation and authoritative state checks stay in one place:

```csharp
using ULinkGame.Server.Sessions;

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
