# ULinkGame

ULinkGame is a game-session infrastructure layer built on top of [ULinkRPC](https://github.com/bruce48x/ulinkrpc).

ULinkRPC gives you typed RPC, generated client/server glue, transports, serializers, and callbacks. ULinkGame starts one level higher: it helps a game client and a .NET game server stay consistent across login, reconnect, realtime traffic, Orleans hosting, and reliable business push messages.

## Why ULinkGame

Online games need more than a request/response RPC pipe. A typical project quickly has to answer questions such as:

- How do I host several RPC endpoints cleanly in the same .NET server?
- How do I combine a control connection with realtime traffic?
- How do I reconnect without losing important business notifications?
- How do I keep Unity, Godot, and plain .NET clients from duplicating the same session-state bookkeeping?
- How do I use Orleans without wiring the same host/bootstrap code in every game?

ULinkGame packages those repeatable pieces while leaving your actual game rules in your project.

## What You Get

`ULinkGame.Server` provides server-side hosting helpers:

- ULinkRPC server lifecycle integration with .NET hosting
- Orleans client and silo integration helpers
- multiple named RPC server configurators for control/realtime endpoints
- a generic reliable push outbox for business-level notifications
- extension points that keep transport and serializer choices in your app

`ULinkGame.Client` provides engine-neutral client helpers:

- reliable push sequence tracking
- duplicate/stale push filtering
- reusable state primitives that work in Unity, Godot, or plain .NET

`ULinkGame.Tool` provides project scaffolding and maintenance commands:

- creates a ULinkRPC-based project through a pinned starter tool
- augments the generated project with ULinkGame server hosting
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

## Install

Install the runtime packages you need:

```powershell
dotnet add package ULinkGame.Server
dotnet add package ULinkGame.Client
```

Install the project tool as a .NET tool:

```powershell
dotnet tool install --global ULinkGame.Tool
```

Create a starter project:

```powershell
ulinkgame-tool new --name MyGame --output ./out --client-engine unity --transport kcp --serializer memorypack
```

The generated project uses ULinkRPC for typed RPC and augments the server with ULinkGame hosting, Orleans integration, and reliable push infrastructure.

## Package Guide

Use `ULinkGame.Server` in your .NET server process when you need ULinkRPC hosting, Orleans integration, or reliable push delivery.

Use `ULinkGame.Client` in client-side code when you need reliable push tracking independent of Unity or Godot.

Use `ULinkGame.Tool` when creating or maintaining a ULinkGame project layout.

## Samples And Design Notes

The repository includes Unity and Godot samples under `samples/`.

For internals and contribution workflow, see [CONTRIBUTING.md](CONTRIBUTING.md).

For design details, see:

- [Reliable push design](docs/RELIABLE_PUSH_DESIGN.md)
- [Package decision notes](docs/ULINKGAME_PACKAGE_DECISION.md)
