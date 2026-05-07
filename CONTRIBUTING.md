# Contributing

This document is for people working on the ULinkGame repository itself. User-facing package information belongs in `README.md`.

## Project Layout

```txt
src/
  ULinkGame.Server/       Server-side hosting, Microsoft Orleans integration, reliable push outbox
  ULinkGame.Client/       Engine-neutral client helpers, currently reliable push tracking
  ULinkGame.Tool/         Project management tool entry point

samples/
  Agar.Unity/             Unity + .NET multiplayer sample
    docs/                 Sample gameplay design and development plan
    tests/                Sample gameplay and server policy tests
  Agar.Godot/             Godot .NET client sample

Tests/
  tests.slnx              Framework test entry point
  ULinkGame.Client.Tests/ Client package unit tests
  ULinkGame.Server.Tests/ Server package unit tests

docs/
  Hugo blog and user-facing article source
```

User-facing articles live in the Hugo site under root `docs/`. Repository-level architectural decisions and package-specific design notes are maintained in this guide when they affect package boundaries, server behavior, client behavior, or sample integration.

## Package Boundaries

### ULinkGame.Server

`ULinkGame.Server` is the server-side framework package. It currently owns:

- hosting helpers for ULinkRPC server lifecycle
- Orleans client and silo integration helpers
- a generic reliable push outbox for business-level server push delivery
- extension points for project-specific RPC server configurators

It should stay infrastructure-oriented. Matchmaking rules, room rules, user DTOs, and gameplay state belong in the game project or sample, not in the framework core.

### ULinkGame.Client

`ULinkGame.Client` is an engine-neutral client helper package. It currently contains reliable push sequence tracking that can be reused by Unity, Godot, or plain .NET clients.

This repository intentionally does not introduce `ULinkGame.Shared` or `ULinkGame.Unity` yet. User-owned contracts still belong in a game `Shared` project, and Unity-specific wrappers should wait until repeated integration code becomes stable enough to justify a package.

### ULinkGame.Tool

`ULinkGame.Tool` is the project tool package. Its command name is:

```bash
ulinkgame-tool
```

It is separate from runtime packages. Runtime code belongs in `ULinkGame.Server` or `ULinkGame.Client`; project scaffolding and maintenance commands belong in the tool.

## Package Decision

### Background

The framework started as a thin server-hosting layer: it wired ULinkRPC servers, Microsoft Orleans, dependency injection, and process lifetime. Reliable business push changes that boundary. The framework now owns mechanics that must be understood by both sides of a game session:

- reconnect versus new-session decisions
- business push sequencing
- client acknowledgement semantics
- replay after reconnect
- state-mismatch handling when the server no longer has compatible session state

The old `Host` name now undersells the scope and can imply a server-only library.

### Decision

Rename the framework family to `ULinkGame`.

The first package split is:

- `ULinkGame.Server`
- `ULinkGame.Client`

Do not introduce `ULinkGame.Shared` yet.

Do not introduce `ULinkGame.Unity` yet.

### Why ULinkGame

`ULinkGame` clearly communicates that this layer is above raw RPC and standalone actor hosting, and is intended for game networking workflows. The relationship should be:

- `ULinkRPC`: transport, serialization, RPC calls, and generated bindings
- `Microsoft Orleans`: distributed actors, clustering, placement, and grain state
- `ULinkGame`: game-session infrastructure that integrates ULinkRPC and Microsoft Orleans
- user game code: matchmaking, room rules, gameplay state, rewards, inventory, and other domain features

This keeps the product line understandable without forcing a thick game framework.

### Why Not ULinkGame.Shared Now

A third shared package raises the first-time learning cost. Users already have:

- `ULinkRPC`
- their own shared RPC contract project
- server code
- client code

Adding `ULinkGame.Shared` too early creates a naming collision with user-owned `Shared` projects and makes it unclear where business DTOs should live.

For now, shared business contracts should remain in the user's own shared project. Examples:

- login request/reply DTOs
- matchmaking status payloads
- reliable sequence fields on business messages
- app-specific result codes

If cross-side framework abstractions become stable and numerous, introduce `ULinkGame.Abstractions` later. Prefer `Abstractions` over `Shared`, because it communicates framework-owned contracts instead of user-owned game DTOs.

### Why Not ULinkGame.Unity Now

Unity-specific integration is useful, but it should not be the first client package. The reusable core should not depend on:

- `MonoBehaviour`
- Unity main-thread APIs
- `Time.time`
- Unity logging
- Unity assembly definition layout

The first client package should be a plain .NET library. Unity projects can consume it through normal package/import mechanisms while keeping Unity-specific glue in the sample or in the user's project.

`ULinkGame.Unity` can be added later only when repeated Unity-specific integration code becomes stable enough to justify a package.

### First Client Library Boundary

`ULinkGame.Client` should own client-side mechanisms that are not engine-specific:

- latest applied reliable push sequence tracking
- duplicate reliable message detection
- ack decision helpers
- state-mismatch result handling
- reconnect state transitions that are independent of UI rendering

It should not own:

- Unity scene state
- UI text
- gameplay-specific callbacks such as `MatchmakingStatusUpdate`
- transport creation details unless they can be expressed through small interfaces

Unity sample code should remain responsible for:

- copying RPC DTOs into a main-thread inbox
- mutating Unity UI and scene state on the main thread
- choosing how to display reconnect/new-session outcomes
- calling generated RPC clients

### Migration Plan

1. Keep reusable framework code under `src/ULinkGame.Server`, `src/ULinkGame.Client`, and `src/ULinkGame.Tool`.
2. Keep sample-owned `Shared`, `Server`, and `Client` projects under `samples/Agar.Unity`.
3. Keep business DTOs in the sample or consuming game's `Shared` project until a real `ULinkGame.Abstractions` need appears.
4. Keep cross-package framework decisions in this guide, package-specific design with the owning package when the scope is local, and sample design under `samples/Agar.Unity/docs`.
5. Replace sample-local reliable sequence bookkeeping with `ULinkGame.Client` where practical.

### Compatibility Note

During early development, breaking namespace and project-name changes are acceptable. Once packaged, add compatibility shims or a migration guide only if external users already depend on older package names.

## Samples

The repository currently contains two sample clients:

```txt
samples/Agar.Unity/
  Shared/  MemoryPack contracts and shared gameplay kernel
  Server/  .NET server, Orleans silo, WebSocket control plane, KCP realtime plane
  Client/  Unity client

samples/Agar.Godot/
  Godot .NET client playground that references src/ULinkGame.Client and Agar.Unity/Shared
```

`samples/Agar.Unity` demonstrates:

- a Unity client plus .NET server game layout
- WebSocket as the long-lived control connection
- KCP for realtime gameplay traffic
- reconnect-aware login flow
- business-level reliable push for server notifications
- an agar-style arena built on a shared simulation kernel

`samples/Agar.Godot` is intentionally smaller. It is an offline Godot .NET client playground that reuses the shared agar gameplay kernel and `ULinkGame.Client` reliable push helpers.

Sample-specific documentation and local infrastructure live with the sample:

- `samples/Agar.Unity/README.md`
- `samples/Agar.Unity/docs/GAMEPLAY_DESIGN.md`
- `samples/Agar.Unity/docs/DEVELOPMENT_PLAN.md`
- `samples/Agar.Unity/docker-compose.yml`
- `samples/Agar.Unity/.env.example`
- `samples/Agar.Unity/dotnet-tools.json`
- `samples/Agar.Unity/infra/`

Run the sample server pieces separately:

```powershell
dotnet run --project samples/Agar.Unity/Server/Silo/Silo.csproj
dotnet run --project samples/Agar.Unity/Server/Server/Server.csproj
```

Open `samples/Agar.Unity/Client` in Unity for the client.

Open `samples/Agar.Godot` in Godot 4 .NET for the Godot client playground.

## Build And Test

Build framework projects:

```powershell
dotnet build src/ULinkGame.Server/ULinkGame.Server.csproj
dotnet build src/ULinkGame.Client/ULinkGame.Client.csproj
dotnet build src/ULinkGame.Tool/ULinkGame.Tool.csproj
```

Build and run unit tests:

```powershell
dotnet test Tests/tests.slnx
```

Sample-specific tests live with their sample, for example `samples/Agar.Unity/tests/BusinessLogic.Tests`.

The Unity project may generate local `Library`, `Temp`, `obj`, and restored NuGet package folders. These are ignored and should not be committed.

## Design Boundary

ULinkGame should not become a full game business framework. Keep the boundary narrow:

- Framework: connection lifecycle, host integration, session infrastructure, reliable push mechanics, reusable client state helpers.
- Game project: accounts, matchmaking policy, room rules, gameplay simulation, UI, persistence schema, and product-specific DTOs.

When a capability is only useful to one sample, keep it under that sample in `samples/`. Move it into `src` only when it is demonstrably reusable across games.

## Reliable Business Push Design

### Problem

Server callbacks are currently fire-and-forget at the business layer. A transport can report that a push write was accepted, while the target player reconnects before the client applies the business event.

Example:

1. Players A and B enter matchmaking.
2. The server creates a room and pushes `Matched` to both clients.
3. A receives and handles the push.
4. B reconnects during the push window.
5. The old connection is gone, but the server has no business-level proof that B handled `Matched`.
6. B may stay on the waiting screen forever.

The transport can reduce packet loss, but it cannot prove that the client applied a business event after a reconnect. The fix needs to be above transport: reliable, idempotent business push.

### Recommended Model

Use at-least-once delivery with per-player monotonic sequence numbers.

This is a better fit than trying to implement exactly-once delivery:

- Exactly-once is not realistic across reconnects, retries, client crashes, and server failover.
- At-least-once plus idempotent client handling is predictable and common in game control-plane flows.
- Sequence numbers let clients discard duplicates and let servers prune acknowledged messages.
- The mechanism is generic enough for `ULinkGame.Server`; matchmaking, rooms, mail, rewards, and other features can opt in without entering host core as business concepts.

### Layering

`ULinkGame.Server` owns the generic mechanism:

- allocate a per-owner sequence number
- store pending reliable push records
- replay pending records after reconnect
- accept acknowledgements and prune old records
- apply retention and pending-count limits

Business code owns business semantics:

- choose which push messages require reliability
- include the reliable sequence in its payload
- expose an ack RPC or piggyback ack on an existing request
- make client handlers idempotent by ignoring already applied sequence numbers

This keeps `ULinkGame.Server` as host infrastructure rather than a matchmaking or room framework.

### Message Flow

Publishing a reliable push:

1. Business code asks `IReliablePushOutbox` to publish a payload for `ownerKey`.
2. The outbox assigns `sequence = lastSequence(ownerKey) + 1`.
3. The outbox stores `{ ownerKey, sequence, kind, payload }`.
4. The business delivery delegate sends the payload to the current callback, including `sequence`.
5. If the current callback is missing or disconnected, the record stays pending.

Acknowledging:

1. The client applies the business message.
2. The client sends the latest applied sequence to the server.
3. The outbox removes records with `sequence <= latestAppliedSequence`.

Reconnecting:

1. The client reconnects through normal login/resume flow.
2. The server rebinds the new callback.
3. The server calls `ReplayPendingAsync(ownerKey, deliver)`.
4. Pending records are pushed again through the new callback.
5. The client ignores duplicates whose sequence is not newer than its local latest applied sequence.

### State Mismatch

Reliable push must also handle the case where the client believes it is resuming a valid session, but the server no longer has compatible state. This can happen when:

- the client stayed offline beyond the reconnect grace period
- the gateway process restarted and lost its in-memory outbox
- server-side cleanup removed the session before the client returned

The server should not silently accept this as a successful reconnect. It must return an explicit "state lost" result and require a new session.

There are two detection points:

- `LoginAsync(reconnect: true)`: before accepting the reconnect, the server verifies that the session still exists and that the token matches. If not, it returns a reconnect-state-lost code.
- reliable push ack: if the client acknowledges a sequence greater than the server's last known sequence, the server knows the client has state from a different or expired server session. The ack response should request a new session.

Client behavior:

1. Stop treating the current flow as recoverable.
2. Clear cached realtime room, pending callbacks, and latest reliable sequence.
3. Start a normal login/new-session flow instead of retrying reconnect.
4. Return the player to a coherent lobby or login state; do not leave them on a stale matchmaking or in-match screen.

### Persistence

The first implementation is an in-memory outbox. It solves short reconnect gaps on the same gateway process, which is the current failure mode.

For production-grade cross-process recovery, replace the store behind `IReliablePushOutbox` with durable storage:

- Redis sorted sets or streams keyed by player id
- Orleans grain state keyed by player id
- SQL table with `(ownerKey, sequence)` primary key

The public mechanism should not change when the store becomes durable.

### Retention

Reliable push is not an infinite event log.

Defaults:

- pending retention: 2 minutes
- max pending records per owner: 256

If a client does not reconnect and ack within the retention window, business code must recover via authoritative state queries or force the player back to a coherent screen.

### Client Rules

Clients must:

- store the latest applied reliable sequence per player/session
- apply messages only when `sequence > latestAppliedSequence`
- ack only after the UI/session state transition has been applied
- tolerate receiving the same business message more than once

### Current Sample Integration

The sample uses reliable push for `MatchmakingStatusUpdate`, because missing `Matched` blocks the user flow.

`WorldState` is intentionally not reliable through this mechanism. It is high-frequency realtime state and should be replaced by newer snapshots, not replayed as history.

Implementation points:

- `ULinkGame.Server.ReliablePush.IReliablePushOutbox` is the generic host-level abstraction.
- `ULinkGame.Server.ReliablePush.InMemoryReliablePushOutbox` is the current short-gap implementation.
- `Server.Services.ReliableMatchmakingPublisher` adapts matchmaking status pushes to the generic outbox.
- `IPlayerService.AckReliablePushAsync` is the sample ack RPC.
- `MatchmakingStatusUpdate.ReliableSequence` carries the sequence to the Unity client.
- The Unity client acks after applying a newer sequence and ignores duplicate older sequences.
