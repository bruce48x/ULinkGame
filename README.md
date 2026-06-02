# ULinkGame

A C# game server framework. **Shared code**, **hot reload**, **actor model** — one command to a running server.

⚡ Shared Contracts &nbsp; 🔥 Hot Reload (C#) &nbsp; 🎮 Unity + Godot &nbsp; 🔁 Reliable Push &nbsp; 🏗️ CLI Scaffolding &nbsp; 🛡️ Runtime Guardrails

## What is ULinkGame

ULinkGame is an actor-based distributed game framework for C#. Define your network contracts and state types in one `Shared` project, write your game logic on the server, and **hot-reload** it without restarting.

- **Shared contracts.** RPC interfaces, DTOs, session types, and state definitions live in one `Shared` project. Server and Unity/Godot clients compile the same source — no duplication, no drift.
- **Hot reload.** Edit game logic, save, and the server picks it up automatically. Uses `AssemblyLoadContext` — pure C#, no Lua, no JS, no DSL.
- **Easy to start.** One CLI command scaffolds a complete project with server, hotfix, and client integration. `dotnet run` and you are live.

Built on [ULinkRPC](https://github.com/bruce48x/ulinkrpc) for communication and [ULinkActor](https://www.nuget.org/packages/ULinkActor) for process-local actor execution.

## Quick Start

```bash
dotnet tool install --global ULinkGame.Tool
ulinkgame-tool new --name MyGame --client-engine unity --transport tcp --serializer memorypack
cd MyGame
dotnet run --project "Server/Server/Server.csproj"
```

One command creates a project with hot-reloadable game logic, shared contracts, and a Unity or Godot client ready to connect. No manual wiring.

## Shared Contracts: Define Once

Server and client share the same network contracts, DTOs, and state types. Define them in the `Shared` project — both sides compile from the same source.

```csharp
// Shared/Gameplay/GameRules.cs — compiled for server AND client

[HotfixState]
public sealed partial class GameRulesState
{
    private int _minimumScore = 1;

    public GameRuleResult Evaluate(GameRuleInput input)
    {
        // Server: dispatched to the hotfix assembly at runtime
        // Client: calls EvaluateStable directly
        return HotfixDispatch.Invoke<GameRulesState, GameRuleInput, GameRuleResult>(
            nameof(Evaluate), this, input);
    }

    internal GameRuleResult EvaluateStable(GameRuleInput input)
    {
        if (string.IsNullOrWhiteSpace(input.PlayerId))
        {
            return new GameRuleResult { Accepted = false, Reason = "PlayerId required" };
        }
        return input.Score >= _minimumScore
            ? new GameRuleResult { Accepted = true }
            : new GameRuleResult { Accepted = false, Reason = "Score too low" };
    }
}
```

```csharp
// Server.Hotfix/Gameplay/GameRulesSystem.cs — server-only, hot-reloadable

[FriendOf(typeof(GameRulesState))]
[HotfixSystemOf(typeof(GameRulesState))]
public static class GameRulesSystem
{
    public static GameRuleResult Evaluate(this GameRulesState self, GameRuleInput input)
    {
        // Your live game logic — change this, save, and it reloads automatically
        return self.EvaluateStable(input);
    }
}
```

Change `GameRulesSystem.Evaluate`, rebuild the hotfix project, and the server reloads it. No restart. No downtime. Clients never see the hotfix code.

## Hot Reload

ULinkGame loads hotfix assemblies into a collectible `AssemblyLoadContext`. The file watcher detects changes, loads the new DLL, rebuilds the dispatch table, and unloads the old assembly — all atomically.

```csharp
// In Program.cs — register hotfix and file watching
var hotfixDirectory = ResolveHotfixDirectory("../../../../Hotfix/bin/Debug/net10.0");
builder.Services.AddULinkGameHotfix(
    new CurrentDirectoryHotfixAssemblySource(hotfixDirectory, "Server.Hotfix.dll"),
    sharedAssemblyNames: ["Shared"]);

builder.Services.AddULinkGameHotfixFileWatcher();
```

|  | Traditional | ULinkGame |
|---|---|---|
| Language | Lua, JS, or custom DSL | C# — same language as the rest of your server |
| Debugging | Separate debugger, type mismatches at runtime | Same IDE, same debugger, compile-time safety |
| Deploy | Restart server or reload entire VM | Save file, auto reload in under a second |
| Registration | Manual dispatch wiring | `[HotfixSystemOf]` attribute + source generator |

## Dual-Channel Networking

Control messages over WebSocket, real-time state over KCP. Built in, not bolted on.

```csharp
// Server binds two channels per session
await server.BindEndpointAsync<IControlCallback>(
    session, GameEndpointName.Control, controlConnectionId, controlCallback, ct);
await server.BindEndpointAsync<IRealtimeCallback>(
    session, GameEndpointName.Realtime, realtimeConnectionId, realtimeCallback, ct);
```

Your game gets a reliable channel for login, matchmaking, and leaderboard, plus a low-latency channel for input and state sync — with the same session identity across both.

## Reliable Push

Players disconnect during critical moments: login, matchmaking, room entry, settlement. Reliable push delivers important notifications at-least-once, with monotonic sequence numbers and automatic duplicate filtering.

**Server:**
```csharp
await server.PublishReliablePushAsync<IPlayerCallback, MatchFound>(
    session, GameEndpointName.Control, "match_found",
    new MatchFound { RoomId = roomId },
    (callback, payload) => callback.OnMatchFound(payload));
```

**Client:**
```csharp
await client.ProcessReliablePushAsync(sequence, payload,
    apply: (MatchFound p, CancellationToken ct) => { /* handle */ return Task.CompletedTask; },
    acknowledge: ack => client.AcknowledgeAsync(ack));
```

The inbox tracks the highest acknowledged sequence, detects gaps, and requests replay automatically.

## Actor Model

Gameplay state runs inside actors — single-threaded, mailbox-ordered execution. No locks, no races.

```csharp
[ActorName("room")]
public class RoomActor : Actor<RoomId>
{
    [ActorMethod("join")]
    public ValueTask<JoinResult> JoinAsync(JoinRequest request, CancellationToken ct)
    {
        _players.Add(request.PlayerId);
        return new(new JoinResult { Accepted = true });
    }
}

// Typed selectors generated at compile time — zero reflection
var rooms = provider.GetRequiredService<RoomActors>();
await rooms.Get(roomId).JoinAsync(request, ct);        // Distributed
await rooms.Local(roomId).JoinAsync(request, ct);       // Current node only
await rooms.Remote(nodeId, roomId).JoinAsync(request, ct); // Pinned to node
```

Source generators produce `RoomActors` with `Get`, `Local`, and `Remote` selectors. No reflection, no string-based dispatch.

## Feature / Role System

Assemble server capabilities from composable features. One role in development, many roles in production.

```csharp
builder.Services.AddFeatures(builder.Configuration, features =>
{
    features.AddRole<GatewayRole>();
    features.AddRole<MatchmakingRole>();
    features.AddRole<RoomRole>();
});
```

## Runtime Guardrails

Validate your configuration before starting:

```bash
dotnet run --project "Server/Server/Server.csproj" -- --ulinkgame-check
```

Catches missing endpoints, bad cluster topology, and hotfix source misconfiguration before they reach production. Three profiles: `Development`, `Compose`, `Production`.

## Cluster

Scale beyond a single process. Actors are addressable across nodes via a directory service.

```csharp
// Same API, single node or cluster — the directory handles routing
await rooms.Get(roomId).JoinAsync(request, ct);
```

In-memory directory for development, SQL-backed (MySQL / PostgreSQL) for production.

## What It Does Not Do

ULinkGame is infrastructure, not a full game business framework. It does not choose your account model, matchmaking policy, room rules, gameplay simulation, persistence schema, or UI architecture. Those decisions belong to your game.

## Packages

| Package | Description |
|---|---|
| `ULinkGame.Abstractions` | Shared types: sessions, endpoints, reliable push primitives |
| `ULinkGame.Client` | Client helpers: reconnect state, push inbox, session tracking |
| `ULinkGame.Server` | Server runtime: actor execution, sessions, reliable push outbox |
| `ULinkGame.Cluster` | Cluster routing contracts and directory primitives |
| `ULinkGame.Tool` | CLI scaffolding tool (`ulinkgame-tool new`) |

## Platform Support

| Platform | Status |
|---|---|
| .NET 10 (server) | Full |
| .NET Standard 2.1 (shared / client) | Full |
| Unity 2021.3+ | Full |
| Godot 4.x (.NET) | Full |
| Windows / Linux / macOS | Full |

## Samples

Full multiplayer game samples (agar.io-style):

- [`samples/Agar.Unity`](samples/Agar.Unity) — Unity client with dual-channel (WebSocket + KCP)
- [`samples/Agar.Godot`](samples/Agar.Godot) — Godot client, same server
- [`samples/Cluster.TwoNode`](samples/Cluster.TwoNode) — Multi-process cluster with directory

## Further Reading

- [Getting Started](https://bruce48x.github.io/ULinkGame/posts/ulinkgame-getting-started/)
- [Design Philosophy](docs/design-philosophy.md)
- [Feature / Role System](docs/feature-role.md)
- [Runtime Guardrails](docs/ulinkgame-runtime-guardrails.md)
- [ULinkRPC](https://github.com/bruce48x/ulinkrpc)
