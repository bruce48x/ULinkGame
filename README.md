# ULinkGame

ULinkGame is a game-session infrastructure layer built on [ULinkRPC](https://github.com/bruce48x/ulinkrpc), [ULinkActor](https://www.nuget.org/packages/ULinkActor), and session/reconnect primitives for online game servers and clients.

ULinkRPC provides typed RPC, generated client/server glue, transports, serializers, and callbacks. ULinkActor provides the foundational process-local actor/mailbox runtime and source-generated typed actor helpers through [ULinkActor.SourceGenerator](https://www.nuget.org/packages/ULinkActor.SourceGenerator). ULinkGame owns the higher-level game infrastructure: session lifecycle, endpoint callback binding, reconnect semantics, realtime-friendly server structure, and reliable business push messages.

ULinkGame is not an Orleans wrapper. It exists because Orleans-style distributed actor hosting is too heavy and too enterprise-oriented for the game server workflows this framework targets: process-local rooms, battles, matchmaking queues, and short-path state execution that need predictable mailbox behavior on edge processes.

## Why ULinkGame

Online games need more than a request/response RPC pipe. A typical project quickly has to answer questions such as:

- How do I host several RPC endpoints cleanly in the same .NET server?
- How do I combine a control connection with realtime traffic?
- How do I reconnect without losing important business notifications?
- How do I keep Unity, Godot, and plain .NET clients from duplicating the same session-state bookkeeping?
- How do I run realtime battle state on the edge process without splitting gameplay across two frameworks?

ULinkGame packages those repeatable pieces while leaving your actual game rules in your project.

## What You Get

`ULinkGame.Abstractions` provides cross-side framework primitives:

- shared session identity
- shared endpoint names
- reliable push sequence values
- reliable push acknowledgement outcomes
- session resume outcomes

`ULinkGame.Server` provides server-side hosting helpers:

- one main server entry point for sessions, endpoint bindings, and reliable push
- ULinkActor-based process-local game state execution for room, battle, and service runtime code
- ULinkRPC server lifecycle integration with .NET hosting
- multiple named RPC server configurators for control/realtime endpoints
- a generic reliable push outbox for business-level notifications
- extension points that keep transport and serializer choices in your app

`ULinkGame.Client` provides engine-neutral client helpers:

- one main client entry point for reconnect state and reliable push processing
- reliable push sequence tracking
- duplicate/stale push filtering
- reusable state primitives that work in Unity, Godot, or plain .NET

`ULinkGame.Tool` provides project scaffolding and maintenance commands:

- creates a ULinkRPC-based project through a pinned starter tool and prepares it for ULinkGame server hosting
- augments the generated project with ULinkGame server/client runtime packages
- writes a local tool manifest for repeatable code generation

## What It Does Not Do

ULinkGame is not a full game business framework. It does not decide your:

- account model
- matchmaking policy
- room rules
- gameplay simulation
- persistence schema
- product-specific DTOs
- Unity or Godot UI architecture

Those belong in your game. ULinkGame stays focused on the infrastructure that many online games have to rebuild.

## Create A Project

Use `ULinkGame.Tool` to create a starter project instead of wiring the runtime packages by hand:

```powershell
dotnet tool install --global ULinkGame.Tool
ulinkgame-tool new --name MyGame --client-engine unity --transport kcp --serializer memorypack --persistence none
```

The tool creates a ULinkRPC-based project, prepares ULinkGame server hosting, adds ULinkGame server/client integration, and writes a local tool manifest for repeatable code generation. By default it generates one RPC endpoint using the selected transport; pass `--network-profile realtime` when you want separate control and realtime endpoints.

Run code generation later from the generated project:

```powershell
ulinkgame-tool codegen
```

For the full walkthrough, see [ULinkGame getting started](https://bruce48x.github.io/ULinkGame/posts/ulinkgame-getting-started/).

## Package Guide

Use `ULinkGame.Abstractions` in shared code when you need framework-owned session and reliable push primitives.

Use `ULinkGame.Server` in your .NET server process when you need ULinkRPC hosting, ULinkActor-based game-state execution, session lifecycle, endpoint callback bindings, or reliable push delivery.

Use `ULinkGame.Client` in client-side code when you need reconnect state and reliable push tracking independent of Unity or Godot.

Use `ULinkGame.Tool` when creating or maintaining a ULinkGame project layout.

## Samples And Design Notes

The repository includes Unity and Godot samples under `samples/`.

For internals and contribution workflow, see [CONTRIBUTING.md](CONTRIBUTING.md).

For user-facing articles, see the Hugo blog at [bruce48x.github.io/ULinkGame](https://bruce48x.github.io/ULinkGame/).

For implementation design notes, see:

- [Reliable push design](CONTRIBUTING.md#reliable-business-push-design)
- [Package decision notes](CONTRIBUTING.md#package-decision)
