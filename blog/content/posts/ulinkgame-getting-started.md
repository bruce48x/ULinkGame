---
title: "Getting Started With ULinkGame: Build Game Projects With Sessions, Reconnects, And Reliable Push"
date: 2026-05-07T11:20:00+08:00
summary: "Build a C# client-and-server game project, share contracts and code, start the server, open the Unity or Godot client, and make your first service call."
tags:
  - ulinkgame
  - ulinkrpc
  - ulinkactor
  - unity
  - godot
  - dotnet
  - tutorial
categories:
  - Tutorial
---

Online games usually need these basics very quickly:

- players need a session after login
- clients need to reconnect after a disconnect
- important server notifications must not disappear during a disconnect window
- a .NET server usually needs a Gateway process for client ingress, while room, battle, or service state runs in a serialized state runtime
- Unity, Godot, and plain .NET clients should not each reimplement reliable-push deduplication

ULinkGame is built for that layer. It does not write your account system, inventory, matchmaking rules, or gameplay logic for you. It gives you a runnable project shape and the session infrastructure that online games repeatedly need.

This framework is intentionally for **C# on both sides**: a .NET server plus a C# client such as Unity, Tuanjie, Godot .NET, or another .NET client. That constraint is the point. When the front end and back end use the same language, you can share contracts, DTOs, validation helpers, protocol constants, and selected gameplay logic instead of maintaining parallel implementations in two stacks.

For small teams, that shared-code model can remove a lot of routine work:

- one set of request and response types
- one set of callback payloads
- one typed contract path for client and server calls
- fewer mismatches between client assumptions and server behavior
- faster iteration when gameplay features change

The goal of this guide is simple: generate a project, start the server, run the client, then add one small feature in the right place.

## Prerequisites

Before you start, install the **.NET 10 SDK**:

- Download: https://dotnet.microsoft.com/en-us/download/dotnet/10.0

If you want to generate a Unity client, you also need:

- Unity 2022 LTS, or a compatible Unity version
- a Unity project that uses C# gameplay scripts
- after opening the Unity project for the first time, run `NuGet -> Restore Packages`

If you want to generate a Godot client, you also need:

- Godot 4.x .NET

Non-C# clients are outside this framework's starter path. If your game client is JavaScript, TypeScript, Lua, C++, Java, Swift, or another stack, you lose the main benefit this guide is built around: sharing C# code directly between client and server.

ULinkGame's project tool reuses `ULinkRPC.Starter` to generate the base RPC project, so install both tools:

```bash
dotnet tool install -g ULinkRPC.Starter
dotnet tool install -g ULinkGame.Tool
```

If they are already installed, update them:

```bash
dotnet tool update -g ULinkRPC.Starter
dotnet tool update -g ULinkGame.Tool
```

## Quick Start

For a first integration, use the easiest-to-debug combination:

- `unity`
- `websocket`
- `json`
- default cluster-ready server scaffolding
- default persistence, which means no business database

Run:

```bash
ulinkgame-tool new --name MyGame --client-engine unity --transport websocket --serializer json
cd MyGame
dotnet run --project Server/State/State.csproj
```

Keep the state process running, then open a second terminal:

```bash
cd MyGame
dotnet run --project Server/Server/Server.csproj
```

Then open the client:

- open `MyGame/Client` in Unity
- wait for import to finish
- run `NuGet -> Restore Packages`
- open the default connection test scene
- click Play

If you chose Godot:

```bash
ulinkgame-tool new --name MyGame --client-engine godot --transport websocket --serializer json
cd MyGame
dotnet run --project Server/State/State.csproj
```

Then start Gateway in a second terminal:

```bash
cd MyGame
dotnet run --project Server/Server/Server.csproj
```

Then:

- open `MyGame/Client` in Godot 4.x .NET
- wait for Godot to generate and restore the C# project
- open the default scene
- click Play

The shortest path is:

**Install both tools -> generate the project -> start the state process -> start Gateway -> open Client -> restore dependencies -> run the default test scene.**

## Understand The Generated Structure

A generated project has four places you will work with most often.

A typical project looks like this:

```text
MyGame/
  Shared/
    Interfaces/
  Server/
    Server.slnx
    Server/
      Server.csproj
      Program.cs
      Services/
    State/
      State.csproj
      Program.cs
  Client/
  ulinkgame.tool.json
```

Keep each layer's responsibility clear:

- `Shared/`
  Shared DTOs, RPC interfaces, callback interfaces, and small cross-side helpers that are safe to use on both client and server.
- `Server/Server/`
  Client-facing service entry points, connection ingress, callback binding, reliable business push, and session integration.
- `Server/State/`
  Authoritative state services.
- `Client/`
  Unity or Godot project files and game scripts.
- `ulinkgame.tool.json`
  ULinkGame project options.

The easiest beginner mistake is putting everything into Gateway. A sturdier split is:

- put network connections and RPC ingress in Gateway
- put long-lived state and serialized logic in state actors
- keep shared contracts limited to DTOs and interfaces, not server implementations

## Daily Development Flow

Most feature work follows the same path:

- define the service contract and DTOs in `Shared/Interfaces/` once
- build or run the project normally after contract changes
- implement the service in `Server/Server/Services/`
- move long-lived state into the state process when the feature needs authoritative state
- call the typed API from Unity, Godot, or a plain .NET client
- add reliable push and acknowledgements only for notifications that must survive reconnects

The efficiency comes from avoiding duplicate definitions. The same C# contract that describes a server method also gives the client a typed call surface. The same payload class can be used by both sides, so a feature change usually starts with one shared edit instead of separate client and server protocol updates.

The usual business-development flow is:

1. Define RPC contracts in `Shared/Interfaces/`.
2. Build the server or recompile the client after contract changes.
3. Implement server logic in `Server/Server/Services/` or in state actors.
4. Call the typed `RpcApi` from the client.
5. Add reliable push and acknowledgements when you need important server notifications.

## Choose Project Options

Common `ulinkgame-tool new` options look like this:

```bash
ulinkgame-tool new --name MyGame \
  --client-engine unity \
  --transport websocket \
  --serializer json
```

Client engine options:

- `unity`
- `unity-cn`
- `tuanjie`
- `godot`

Transport options:

- `websocket`
- `tcp`
- `kcp`

Serializer options:

- `json`
- `memorypack`

Cluster scaffolding is generated by default. The tool no longer requires a network profile argument.

Persistence options:

- `none`
  Default local-development shape, with no business database assumed. You can omit `--persistence` when you want this default.
- `postgres`
  Generates PostgreSQL connection configuration and package references.
- `mysql`
  Generates MySQL connection configuration and package references.

For a first integration, start with:

```bash
ulinkgame-tool new --name MyGame --client-engine unity --transport websocket --serializer json
```

After the default connection test works, consider:

```bash
ulinkgame-tool new --name MyGame --client-engine unity --transport kcp --serializer memorypack --persistence postgres
```

Do not enable `kcp`, `memorypack`, and database persistence all at once on day one. Get the smallest path working first; upgrading later will be much easier.

## Start The Server

Generated projects usually have two server processes:

- `Gateway`
  The RPC gateway, responsible for client connections and service-call ingress.
- state process
  Hosts ULinkActor-based room, battle, or long-lived service state.

Start the state process first:

```bash
cd MyGame
dotnet run --project Server/State/State.csproj
```

Then start Gateway:

```bash
cd MyGame
dotnet run --project Server/Server/Server.csproj
```

The default `websocket + json` setup starts one WebSocket RPC endpoint on Gateway and includes cluster-ready server configuration. The default client test script connects to that endpoint and calls a default service once.

If you changed transport:

- `websocket`
  Best for first integration and browser/WebSocket-friendly environments.
- `tcp`
  Closer to a traditional persistent TCP connection model.
- `kcp`
  Better for later low-latency realtime gameplay, but harder to debug at first.

## Start The Client

Unity and Tuanjie projects live under:

```text
MyGame/Client
```

After opening the project for the first time:

1. Wait for editor import to finish.
2. Wait for NuGetForUnity to import.
3. Run `NuGet -> Restore Packages`.
4. Open the default connection test scene.
5. Confirm that both the state process and Gateway are running.
6. Click Play.

Godot projects also live under:

```text
MyGame/Client
```

For Godot:

1. Open the project with Godot 4.x .NET.
2. Wait for C# project generation and dependency restore.
3. Open the default scene.
4. Confirm that both the state process and Gateway are running.
5. Click Play.

If the client cannot connect, check in this order:

1. Did the state process start successfully?
2. Did Gateway start successfully?
3. Does the transport match the generated project options?
4. Is the WebSocket port already in use?
5. Did Unity run `NuGet -> Restore Packages`?
6. Did anyone manually edit the generated directory?

## A More Practical Extension Example

Assume the default connection test already works and you want to add your first real feature: querying a player profile.

The first step is to change `Shared/Interfaces/`, for example:

```csharp
using System.Threading.Tasks;
using ULinkRPC.Core;

namespace Shared.Interfaces;

public sealed class GetProfileRequest
{
    public long PlayerId { get; set; }
}

public sealed class GetProfileReply
{
    public long PlayerId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int Level { get; set; }
}

[RpcService(2)]
public interface IProfileService
{
    [RpcMethod(1)]
    ValueTask<GetProfileReply> GetProfileAsync(GetProfileRequest request);
}
```

Then build the server:

```bash
dotnet build Server/Server/Server.csproj
```

Next, implement the service under `Server/Server/Services/`. Conceptually, it looks like this:

```csharp
using Shared.Interfaces;

namespace Server.Services;

public sealed class ProfileService : IProfileService
{
    public ValueTask<GetProfileReply> GetProfileAsync(GetProfileRequest request)
    {
        return new ValueTask<GetProfileReply>(new GetProfileReply
        {
            PlayerId = request.PlayerId,
            DisplayName = $"Player {request.PlayerId}",
            Level = 1
        });
    }
}
```

If this profile should be read from authoritative state, `ProfileService` should call a state actor or your project's own state service instead of putting long-lived state directly inside a normal service implementation.

On the client, call the strongly typed API. The exact namespace depends on your project settings, but the shape is:

```csharp
var reply = await rpc.Api.Shared.Profile.GetProfileAsync(
    new GetProfileRequest
    {
        PlayerId = 10001
    });
```

The important path is:

- contracts live in Shared
- Gateway exposes RPC services
- state actors host authoritative state
- Client calls the typed API

## Reconnect And State Lost

Beginners often think reconnect means reconnecting the socket. For online games, that is not enough.

The real questions are:

- is the session brought back by the client still valid?
- does the server still have compatible session state?
- can the reliable push sequence continue?
- can authoritative room, matchmaking, or settlement state be restored?

ULinkGame expresses these outcomes explicitly. Common results can be understood as:

- `Resumed`
  State is compatible. The session can continue, and pending pushes can be replayed.
- `StateRefreshRequired`
  The session is still valid, but the client's local transient state has expired and must be refreshed from an authoritative snapshot.
- `StateLost`
  The server can no longer validate the old state. The client must clear the old session and start again.

The point is to avoid pretending every disconnect can be recovered losslessly. When recovery is not possible, the server should tell the client to enter a new flow.

## Default Cluster Scaffolding

The default generated server scaffolding includes explicit cluster configuration while keeping the first endpoint model small:

- login
- account queries
- inventory
- mail
- shop
- lightweight matchmaking
- low-frequency room state
- turn-based or light realtime gameplay

It generates one RPC endpoint plus cluster package references, environment-variable-friendly cluster settings, and a local health-check path.

## Choose JSON Or MemoryPack

For a first integration, use:

```bash
--transport websocket --serializer json
```

Reasons:

- errors are easier to read
- the transport path is easier to inspect
- Unity's first dependency import has fewer variables
- the default test is faster to get working

Once structure, connection, and business calls are stable, consider:

```bash
--transport websocket --serializer memorypack
```

Or:

```bash
--transport kcp --serializer memorypack
```

`MemoryPack` is better for performance-sensitive phases, but it is not recommended as the default for first-time debugging.

## Choose Persistence

Persistence is optional. If you omit `--persistence`, the generated project uses the same local-development shape as `--persistence none`, with no business database assumed.

If authoritative state or business data needs a database, choose:

```bash
--persistence postgres
```

Or:

```bash
--persistence mysql
```

ULinkGame only generates basic connection configuration and package references. It does not define your business tables.

These still belong to your game:

- account table
- character table
- inventory table
- leaderboard table
- order table
- room history
- battle records

ULinkGame should not take over your business schema.

## Files You Actually Maintain

In daily development, you mostly maintain:

- `Shared/Interfaces/`
  RPC interfaces, DTOs, and callback contracts.
- `Server/Server/Services/`
  RPC service implementations, connection ingress, and reliable push integration.
- `Server/State/`
  Authoritative state and long-lived business state.
- `Client/`
  Unity/Godot game scripts, UI, and scenes.
- `ulinkgame.tool.json`
  Usually only needed when project structure changes.

Do not manually maintain:

- compiler output directories
- intermediate files generated by Unity, Godot, or the build system

Treat generated and intermediate files as build output, not project source code.

## FAQ

### Why Install Both ULinkRPC.Starter And ULinkGame.Tool?

Because ULinkGame.Tool does not reinvent the lower-level RPC project template.

It first calls `ulinkrpc-starter` to generate a base `Shared + Server + Client` project, then adds the ULinkGame-owned pieces:

- `Server/Server`
- `Server/State`
- authoritative state service configuration
- ULinkGame runtime package references
- `ulinkgame.tool.json`
- project generation configuration

### Why Does The Server Usually Have Two Processes?

Because Gateway and the state process have different responsibilities.

Gateway faces client connections and is a good place for the RPC gateway, callbacks, session binding, and reliable push delivery.

The state process faces authoritative state and is a better place for player state, room state, matchmaking queues, leaderboards, and other long-lived state. Its actor/mailbox execution model keeps this state serialized and easier to reason about.

In local development they can run on the same machine. In production, you can scale them separately according to load and deployment boundaries.

### Can I Wire Up Only ULinkGame.Server By Hand?

Yes, but it is not recommended for a first integration.

Beginners should start with `ulinkgame-tool new` because it generates a runnable project structure in one step. After you understand how Gateway, the state process, Shared, and Client relate to each other, manual restructuring is much safer.

### Does ULinkGame Implement Matchmaking And Rooms For Me?

No.

ULinkGame provides infrastructure that features such as matchmaking, rooms, rewards, and mail can use: session, reconnect, reliable push, and host integration.

Matchmaking rules, room rules, gameplay simulation, and product DTOs should still live in your game project.

## What To Read Next

After the default test works, continue in this order:

1. Add your own RPC service, such as `ProfileService` or `InventoryService`.
2. Practice the full `Shared -> Gateway service -> Client call` flow once.
3. Move long-lived state into an in-process state actor.
4. Then add reliable push, reconnect, and state-lost handling.

Reliable push is for important server notifications that should survive a short disconnect window, such as match found, room entered, settlement completed, reward granted, or mail arrived. You do not need to understand the full mechanism before the default project runs.

Related guides:

- [Reliable Business Push: Why Reliable Transport Is Not Enough](/ULinkGame/posts/reliable-business-push/)
- [Deploying A ULinkGame Server To Multiple Linux Machines](/ULinkGame/posts/deploy-ulinkgame-server-linux-multi-machine/)

## Summary

The recommended ULinkGame starting path is clear:

1. Install `ULinkRPC.Starter` and `ULinkGame.Tool`.
2. Generate a project with `ulinkgame-tool new`.
3. First get the default test working with `websocket + json` and default persistence.
4. Develop business code in the `Shared -> Server/StateActor -> Client` order.
5. After the foundation is stable, upgrade to `memorypack`, `kcp`, or database persistence.

Do not rush to change the generated directory structure during the first integration. Get the tool-generated structure running, understand what each layer owns, then replace pieces with your own business code.
