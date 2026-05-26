# Contributing

This document is for people working on the ULinkGame repository itself. User-facing package information belongs in `README.md`.

## Repository Map

```txt
src/
  ULinkGame.Abstractions/  Cross-side framework-owned session and reliable push primitives
  ULinkGame.Server/       Server-side RPC hosting, session lifecycle, reliable push outbox, and ULinkActor-based execution
  ULinkGame.Client/       Engine-neutral client helpers, currently reliable push tracking
  ULinkGame.Cluster/      Optional explicit cluster route contracts and in-memory routing primitives
  ULinkGame.Cluster.ULinkRPC/ ULinkRPC cluster messenger and route-directory adapter
  ULinkGame.Tool/         Project management tool entry point

samples/
  Cluster.Loopback/        Minimal in-memory cluster route and failure-mode sample
  Cluster.TwoNode/         Multi-process ULinkRPC route-directory and node-messenger smoke sample
  Agar.Unity/             Unity + .NET multiplayer sample
    docs/                 Sample gameplay design and development plan
    tests/                Sample gameplay and server policy tests
  Agar.Godot/             Godot .NET client sample

Tests/
  tests.slnx              Framework test entry point
  ULinkGame.Client.Tests/ Client package unit tests
  ULinkGame.Cluster.Tests/ Cluster package unit tests
  ULinkGame.Cluster.ULinkRPC.Tests/ ULinkRPC cluster adapter unit and TCP smoke tests
  ULinkGame.Server.Tests/ Server package unit tests

blog/
  Hugo blog and user-facing article source
```

User-facing articles live in the Hugo site under root `blog/`. Do not put internal architecture RFCs, repository design decisions, migration plans, or contributor-only technical notes under `blog/`; keep cross-cutting package decisions and repository architecture notes in this guide. Package-specific design notes are maintained in this guide when they affect package boundaries, server behavior, client behavior, or sample integration.

### Samples

The repository currently contains two cluster infrastructure samples and two game client samples:

```txt
samples/Cluster.Loopback/
  Single-process in-memory cluster route and failure-mode sample

samples/Cluster.TwoNode/
  Multi-process ULinkRPC route-directory and node-messenger smoke sample

samples/Agar.Unity/
  Shared/  MemoryPack contracts and shared gameplay kernel
  Server/  .NET Gateway server with ULinkActor-based state runtime, WebSocket control plane, KCP realtime plane
  Client/  Unity client

samples/Agar.Godot/
  Godot .NET client playground that consumes ULinkGame.Client from NuGet and references Agar.Unity/Shared
```

`samples/Cluster.Loopback` demonstrates local cluster routing without network infrastructure.

`samples/Cluster.TwoNode` starts a route-directory process and worker process, then verifies ULinkRPC-based route registration, local dispatch, remote dispatch, route not found, expired message rejection, timeout, handler unavailable, backpressure, stale registration rejection, clear-by-node-epoch, and node restart with a new epoch.

Run the cross-process cluster smoke sample:

```powershell
dotnet run --project samples/Cluster.TwoNode/Cluster.TwoNode.csproj -- --mode driver
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
- `samples/Agar.Unity/infra/`

Run the sample server:

```powershell
dotnet run --project samples/Agar.Unity/Server/Gateway/Gateway.csproj
```

Open `samples/Agar.Unity/Client` in Unity for the client.

Open `samples/Agar.Godot` in Godot 4 .NET for the Godot client playground.

## Contributor Workflow

### Build And Test

Build framework projects:

```powershell
dotnet build src/ULinkGame.Abstractions/ULinkGame.Abstractions.csproj
dotnet build src/ULinkGame.Server/ULinkGame.Server.csproj
dotnet build src/ULinkGame.Client/ULinkGame.Client.csproj
dotnet build src/ULinkGame.Cluster/ULinkGame.Cluster.csproj
dotnet build src/ULinkGame.Tool/ULinkGame.Tool.csproj
```

Build and run unit tests:

```powershell
dotnet test Tests/tests.slnx
```

Sample-specific tests live with their sample, for example `samples/Agar.Unity/tests/BusinessLogic.Tests`.

The Unity project may generate local `Library`, `Temp`, `obj`, and restored NuGet package folders. These are ignored and should not be committed.

### NuGet Release

Framework packages are published to nuget.org by the `Publish NuGet` GitHub Actions workflow:

```txt
.github/workflows/publish-nuget.yml
```

The workflow runs automatically on pushes to `main` when one of these paths changes:

- `.github/workflows/publish-nuget.yml`
- `Directory.Build.props`
- `NuGet.config`
- `src/**`
- `Tests/**`

The workflow uses .NET `10.0.x`, restores all test and package projects, runs the client and server package tests, packs every project under `src/*/*.csproj`, then pushes all generated `.nupkg` files to nuget.org with `--skip-duplicate`.

The packages currently published by this workflow are:

- `ULinkGame.Abstractions`, versioned in `src/ULinkGame.Abstractions/ULinkGame.Abstractions.csproj`
- `ULinkGame.Client`, versioned in `src/ULinkGame.Client/ULinkGame.Client.csproj`
- `ULinkGame.Server`, versioned in `src/ULinkGame.Server/ULinkGame.Server.csproj`
- `ULinkGame.Cluster`, versioned in `src/ULinkGame.Cluster/ULinkGame.Cluster.csproj`
- `ULinkGame.Cluster.ULinkRPC`, versioned in `src/ULinkGame.Cluster.ULinkRPC/ULinkGame.Cluster.ULinkRPC.csproj`
- `ULinkGame.Tool`, versioned in `src/ULinkGame.Tool/ULinkGame.Tool.csproj`

Release credentials are managed through the GitHub `release` environment. The workflow uses `NuGet/login@v1` with the `NUGET_USER` secret and then passes the action-provided temporary API key to `dotnet nuget push`.

To release a new package version:

1. Update the `<Version>` in the owning `.csproj`.
2. Update `CHANGELOG.md` with the released package id and version.
3. Update generated template constants or sample package references if the released package is consumed by scaffolding or samples.
4. Run the relevant local tests before merging.
5. Merge or push to `main`; the GitHub Actions workflow publishes the packages.

Useful local checks:

```powershell
dotnet test Tests/tests.slnx
dotnet pack src/ULinkGame.Abstractions/ULinkGame.Abstractions.csproj -c Release -o artifacts/nuget
dotnet pack src/ULinkGame.Client/ULinkGame.Client.csproj -c Release -o artifacts/nuget
dotnet pack src/ULinkGame.Server/ULinkGame.Server.csproj -c Release -o artifacts/nuget
dotnet pack src/ULinkGame.Cluster/ULinkGame.Cluster.csproj -c Release -o artifacts/nuget
dotnet pack src/ULinkGame.Cluster.ULinkRPC/ULinkGame.Cluster.ULinkRPC.csproj -c Release -o artifacts/nuget
dotnet pack src/ULinkGame.Tool/ULinkGame.Tool.csproj -c Release -o artifacts/nuget
```

## Package Boundaries

### ULinkGame.Server

`ULinkGame.Server` is the server-side framework package. It currently owns:

- the `IULinkGameServer` main entry point for session, endpoint, and reliable push workflows
- hosting helpers for ULinkRPC server lifecycle
- ULinkActor-based process-local game state execution integration
- a generic reliable push outbox for business-level server push delivery
- extension points for project-specific RPC server configurators

It should stay infrastructure-oriented. `ULinkActor` is a foundational runtime dependency for ULinkGame's actor execution model, while matchmaking rules, room rules, user DTOs, and gameplay state belong in the game project or sample, not in the framework core.

### ULinkGame.Client

`ULinkGame.Client` is an engine-neutral client helper package. It currently contains the `ULinkGameClient` main entry point plus lower-level reliable push and reconnect state helpers that can be reused by Unity, Godot, or plain .NET clients.

### ULinkGame.Abstractions

`ULinkGame.Abstractions` owns cross-side framework concepts that must be named and interpreted the same way by server and client packages:

- `GameSessionKey`
- `GameEndpointName`
- `ReliablePushSequence`
- reliable push acknowledgement outcomes
- session resume outcomes

It must stay small. User-owned contracts still belong in a game `Shared` project, and Unity-specific wrappers should wait until repeated integration code becomes stable enough to justify a package.

### ULinkGame.Cluster

`ULinkGame.Cluster` is the optional cluster routing package. It owns explicit node identity, route identity, route locations, message envelopes, route directory abstractions, router abstractions, and in-memory implementations for tests or local validation.

It must stay transport-neutral and actor-boundary-aware. The package must not provide transparent remote actor references, actor migration, durable route storage, Redis-specific state, service discovery, production transport, or gameplay concepts. Production adapters should be added only after the in-memory contract proves route lookup, expiration, local dispatch, remote dispatch, backpressure, and trace propagation.

`ULinkGame.Cluster.ULinkRPC` is the first transport adapter package. It owns the ULinkRPC method contract, client-side node messenger, client cache over ULinkRPC transports, endpoint parsing, TCP transport factory, server-side binder for internal node traffic, and a ULinkRPC-managed remote route directory client/binder. It must not own durable route storage, service discovery, durable queues, gameplay DTOs, actor migration, or transparent remote actor clients. Additional concrete transports must come with cross-process smoke tests.

### ULinkGame.Tool

`ULinkGame.Tool` is the project tool package. Its command name is:

```bash
ulinkgame-tool
```

It is separate from runtime packages. Runtime code belongs in `ULinkGame.Server` or `ULinkGame.Client`; project scaffolding and maintenance commands belong in the tool.

Package README files under `src/ULinkGame.Abstractions`, `src/ULinkGame.Tool`, `src/ULinkGame.Server`, and `src/ULinkGame.Client` are user-facing package documentation. Keep contributor-only implementation policy, maintenance boundaries, release process, and design decisions in this `CONTRIBUTING.md` file instead of package README files.

#### ULinkGame.Tool Starter Boundary

In this repository's tool documentation, `starter` means `ulinkrpc-starter`, not `ULinkGame.Tool` or any internal ULinkGame scaffolding phase.

`ULinkGame.Tool` delegates the base project shape to `ulinkrpc-starter`. Treat `ulinkrpc-starter` generated ULinkRPC content as owned by `ulinkrpc-starter`, not by ULinkGame.

`ULinkGame.Tool` must not rewrite, replace, or version-pin starter-owned content:

- `ULinkRPC.*` package references and versions
- Unity, Tuanjie, Godot, or plain .NET client project structure
- ULinkRPC source-generator package references and generated namespace settings
- serializer and transport package selection beyond forwarding the user's `new` command options to `ulinkrpc-starter`

When `ulinkgame-tool new` augments a generated project, it should preserve starter output and add only ULinkGame-owned infrastructure:

- `ULinkGame.Server` and `ULinkGame.Client` package references when needed
- ULinkGame gateway hosting projects and configuration
- `ULinkActor` package references for process-local actor execution
- ULinkGame-specific server startup, gateway, and tool configuration
- project metadata that keeps ULinkGame-owned options visible without reintroducing manual RPC code generation

If a generated project needs a different `ULinkRPC.*` package version or client layout, fix `ulinkrpc-starter` first and then update the `ulinkrpc-starter` version consumed by `ULinkGame.Tool`.

## Product Line Boundaries

`ULinkGame` clearly communicates that this layer is above raw RPC and standalone actor hosting, and is intended for game networking workflows. The relationship should be:

- `ULinkRPC`: transport, serialization, RPC calls, and generated bindings
- `ULinkActor`: process-local actor identity, mailbox execution, timers, backpressure, diagnostics, and source-generated typed spawn helpers
- `ULinkGame`: game-session infrastructure that integrates ULinkRPC, ULinkActor execution, reconnect, named endpoint hosting, and reliable push
- user game code: matchmaking, room rules, gameplay state, rewards, inventory, and other domain features

This keeps the product line understandable without forcing a thick game framework.

### Shared Contracts

`ULinkGame.Abstractions` exists because session identity, reliable push sequence values, and acknowledgement outcomes are now genuinely shared framework concepts. Keeping these types in either `ULinkGame.Server` or `ULinkGame.Client` makes the opposite side depend on the wrong runtime package.

Do not rename this package to `ULinkGame.Shared`. Users already have:

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

`ULinkGame.Abstractions` communicates framework-owned contracts only. Keep it limited to primitives that both runtime packages need.

### Unity Package Boundary

Unity-specific integration is useful, but it should not be the first client package. The reusable core should not depend on:

- `MonoBehaviour`
- Unity main-thread APIs
- `Time.time`
- Unity logging
- Unity assembly definition layout

The first client package should be a plain .NET library. Unity projects can consume it through normal package/import mechanisms while keeping Unity-specific glue in the sample or in the user's project.

`ULinkGame.Unity` can be added later only when repeated Unity-specific integration code becomes stable enough to justify a package.

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

## Framework Scope

ULinkGame should not become a full game business framework. Keep the boundary narrow:

- Framework: connection lifecycle, host integration, session infrastructure, reliable push mechanics, reusable client state helpers.
- Game project: accounts, matchmaking policy, room rules, gameplay simulation, UI, persistence schema, and product-specific DTOs.

When a capability is only useful to one sample, keep it under that sample in `samples/`. Move it into `src` only when it is demonstrably reusable across games.

### Admission Rules

A concept belongs in ULinkGame only when it is infrastructure, not gameplay semantics; useful across multiple game genres; compatible with low-latency online workflows; and able to expose failure, backpressure, and state mismatch explicitly.

Good candidates:

- session identity and endpoint binding
- reliable business push
- named RPC endpoint hosting
- cluster node identity and route location
- route directory and node messenger abstractions
- diagnostics, health checks, and metrics
- optional tool templates for deployment infrastructure

Bad candidates:

- account schemas
- matchmaking rules
- room rules
- battle or skill systems
- AOI implementation
- inventory, guild, leaderboard policy, rewards, quests, or product DTOs
- Unity/Godot UI architecture

## Runtime Architecture

### Actor Runtime Boundary

`ULinkActor` owns the process-local actor/mailbox runtime. ULinkGame builds on it but should not absorb it.

`ULinkActor` is responsible for:

- actor identity and creation
- in-process message delivery
- mailbox serialization and backpressure
- timers and local scheduling
- source-generated typed actor helpers

ULinkGame is responsible for:

- ULinkRPC hosting integration
- session lifecycle and endpoint callback binding
- reliable business push
- route location and node-to-node messaging integration
- explicit cross-node failure results
- tool templates and operational glue

ULinkGame must not provide transparent remote objects. Cross-node work should stay explicit: send a message, call with a timeout, or return a structured failure such as route not found, stale route, timeout, overloaded, or failed. The API shape must make the node boundary visible so callers cannot accidentally write local-looking code that hides serialization, network latency, retry behavior, queueing, or remote backpressure.

### Endpoint Model

ULinkGame should support multiple named RPC endpoints or channels, but it should not force every game to understand a fixed "control connection plus realtime connection" split.

The reusable framework capability is:

- host several named ULinkRPC servers in the same .NET process
- let projects choose transport, serializer, endpoint names, and lifecycle policy
- provide connection/session lifecycle helpers that can work with one endpoint or several endpoints
- keep logging, health checks, and diagnostics understandable per endpoint

The default user mental model should remain simple: one session endpoint can handle login, normal requests, reliable business push, and reconnect for light online games.

The control/realtime split is only an optional example for games that need high-frequency, low-latency gameplay traffic. In that model:

- the control endpoint handles login, matchmaking, room entry, settlement, low-frequency queries, and reliable business push
- the realtime endpoint handles input, snapshots, and other high-frequency gameplay traffic

This split belongs in samples or templates that explicitly opt into that shape. It should not become a mandatory package concept, and starter output should avoid introducing realtime attach, room runtime, or dual-connection terminology unless the selected project shape needs it.

### Reliable Business Push

#### Problem

Server callbacks are currently fire-and-forget at the business layer. A transport can report that a push write was accepted, while the target player reconnects before the client applies the business event.

Example:

1. Players A and B enter matchmaking.
2. The server creates a room and pushes `Matched` to both clients.
3. A receives and handles the push.
4. B reconnects during the push window.
5. The old connection is gone, but the server has no business-level proof that B handled `Matched`.
6. B may stay on the waiting screen forever.

The transport can reduce packet loss, but it cannot prove that the client applied a business event after a reconnect. The fix needs to be above transport: reliable, idempotent business push.

#### Recommended Model

Use at-least-once delivery with per-player monotonic sequence numbers.

This is a better fit than trying to implement exactly-once delivery:

- Exactly-once is not realistic across reconnects, retries, client crashes, and server failover.
- At-least-once plus idempotent client handling is predictable and common for low-frequency session and business flows.
- Sequence numbers let clients discard duplicates and let servers prune acknowledged messages.
- The mechanism is generic enough for `ULinkGame.Server`; matchmaking, rooms, mail, rewards, and other features can opt in without entering host core as business concepts.

#### Layering

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

#### Message Flow

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

#### State Mismatch

Reliable push must also handle the case where the client believes it is resuming a valid session, but the server no longer has compatible state. This can happen when:

- the client stayed offline beyond the reconnect grace period
- the gateway process restarted and lost its in-memory outbox
- server-side cleanup removed the session before the client returned

The server should not silently accept this as a successful reconnect. It must return an explicit "state lost" result and require a new session.

Prefer authoritative-state refresh before declaring the session lost:

```mermaid
flowchart TD
    A["Client reconnects or sends reliable-push ack"] --> B["Server validates session token and generation"]
    B -->|invalid| L["StateLost / NewSessionRequired"]
    B -->|valid| C["Server compares client sequence and session state"]
    C -->|compatible| R["Resume and replay pending records"]
    C -->|mismatch| D["Can server validate authoritative session state?"]
    D -->|yes| E["StateRefreshRequired"]
    E --> F["Client clears transient state and reliable sequence"]
    F --> G["Client fetches authoritative session snapshot"]
    G --> H["Client resumes lobby, match, room, or settlement from snapshot"]
    D -->|no| L
    L --> I["Client clears cached session, room, match, endpoint binding, and reliable sequence"]
    I --> J["Client starts a new login/session flow"]
```

There are two detection points:

- `LoginAsync(reconnect: true)`: before accepting the reconnect, the server verifies that the session still exists and that the token matches. If not, it returns a reconnect-state-lost code.
- reliable push ack: if the client acknowledges a sequence greater than the server's last known sequence, the server knows the client has state from a different or expired server session. The ack response should request a new session.

Client behavior:

1. Stop treating the current flow as recoverable.
2. Clear cached room, endpoint bindings, pending callbacks, and latest reliable sequence.
3. Start a normal login/new-session flow instead of retrying reconnect.
4. Return the player to a coherent lobby or login state; do not leave them on a stale matchmaking or in-match screen.

#### Persistence

The default outbox is process-local and in-memory. Reliable push is a short-window, low-frequency session/business notification mechanism, not a durable business event log and not the source of truth for game state.

If a server process restarts or otherwise loses the outbox, the server should not pretend that replay is still possible. It should return an explicit state-lost result when reconnect or acknowledgement proves that the client has state the server can no longer validate. The client must then clear local session state, reset reliable sequence tracking, and start a new session or return to a coherent lobby/login flow.

Business code should recover from missing reliable pushes through authoritative state queries when the authoritative state still exists. If the authoritative state is gone, forcing a new session is preferred over replaying stale notifications.

Projects may still replace `IReliablePushOutbox` with a durable implementation for specialized low-frequency business events, but that is a project-specific choice. A durable outbox must preserve consistency with the authoritative business state and absorb the added performance, storage, retention, and operations costs. It should not be the ULinkGame default or a reason to turn framework reliable push into a general event-sourcing system.

#### Retention

Reliable push is not an infinite event log.

Defaults:

- pending retention: 2 minutes
- max pending records per owner: 256

If a client does not reconnect and ack within the retention window, business code must recover via authoritative state queries or force the player back to a coherent screen.

#### Client Rules

Clients must:

- store the latest applied reliable sequence per player/session
- apply messages only when `sequence > latestAppliedSequence`
- ack only after the UI/session state transition has been applied
- tolerate receiving the same business message more than once

Reliable push is for low-frequency business or session transitions where missing one event can block the user flow. High-frequency realtime snapshots should be superseded by newer snapshots, not replayed as reliable history.

### Hotfix Boundary

Hotfix is an engineering goal, but it should not pollute the core actor or session APIs.

Recommended model:

```txt
stable runtime state + replaceable business logic
```

Long-lived mutable state should live in stable runtime-owned types or in explicit serialized state. Replaceable business logic can live in a hotfix assembly and operate on that stable state. Large structural changes, protocol changes, and persistence schema changes should use deployment or migration workflows, not pretend to be safe hotfixes.

First versions should avoid hotfixing:

- actor runtime internals
- serializer protocol structure
- transport protocol structure
- persistent state schema
- low-level schedulers

## Cluster Architecture

### Node And Execution Model

A node is a server process participating in a ULinkGame cluster. The framework should prefer neutral node concepts over business node types. `Gateway`, `State`, `Match`, `Room`, or `Battle` are deployment roles, labels, or sample terms, not core identity types.

Suggested node concepts:

- `NodeId`: stable runtime node identity
- node epoch/generation: changes when the process re-registers after restart
- node exposure: client-facing or internal-only
- node capabilities: actor host, client session host, reliable push host, route directory host, scheduler host
- node endpoints: named addresses for internal or external communication
- node state: starting, ready, draining, suspect, dead

Actor execution remains process-local first. An actor belongs to one local execution domain at a time. Cross-thread or cross-node communication should use message delivery, not shared mutable objects. Actor location is separate from actor identity: stable actor ids should not encode node id, ports, endpoints, business category, or thread id.

Cross-node actor communication should be route-based, not proxy-based. A local actor runtime may expose `TellAsync` or `AskAsync` for process-local actor calls, but remote actor communication should go through an explicitly named cluster API such as `IClusterActorRouter`. That API should require route lookup, timeout, expiration, and result handling at the call site. Do not make a remote actor look like a local actor reference.

If `ULinkActor` internally schedules actors through `LogicThread` or a similar execution lane, that remains a node-local runtime detail. Cluster state should route only to `NodeId` and `RouteLocation`; the receiving node then hands the message to its local actor runtime, which chooses the mailbox or logic thread. Cluster code must not store, expose, or target `LogicThread` identifiers.

### Cluster Location And Messaging

Cluster routing uses two separate responsibilities:

- `IRouteDirectory`: stores `RouteKey -> RouteLocation` with expiration, route generation, and node epoch.
- `IClusterRouter`: applies route lookup, TTL checks, local dispatch, remote dispatch, and backpressure behavior.

Node communication is a lower layer:

- `INodeMessenger`: sends a `ClusterMessage` to a resolved `RouteLocation`.
- `IClusterMessageHandler`: receives a cluster message on the target node and dispatches it locally.

Expected flow:

1. Caller submits a `ClusterMessage` to `IClusterRouter`.
2. Router rejects expired messages before doing directory or network work.
3. Router resolves the current `RouteLocation` through `IRouteDirectory`.
4. If the target node is local, router calls local `IClusterMessageHandler`.
5. If the target node is remote, router calls `INodeMessenger.SendAsync(location, message)`.
6. The receiving adapter handles authentication, TLS, compression, and wire format, then invokes local `IClusterMessageHandler`.

Route locations are generation-aware. A new owner should publish a higher route generation; a restarted node should publish a higher node epoch. The directory must reject stale registrations, support conditional lease refresh by the current owner, and allow clearing routes for a specific node epoch so a restarted node does not accidentally inherit old traffic.

The first implementations provide in-memory directory and loopback messenger behavior for local validation, plus a ULinkRPC-managed remote route directory adapter for multi-process validation. Production adapters should stay pluggable and should not change the transport-neutral `IRouteDirectory` or `IClusterRouter` contracts.

### Production Adapter Decision

The first production adapter direction is the ULinkRPC internal transport adapter in `ULinkGame.Cluster.ULinkRPC`. It contains the protocol, client-side messenger, remote route directory client/binder, client cache, TCP transport factory, and server binder foundation. A real multi-process sample or generated template is still required before making cluster mode a recommended default for users.

Decision:

- Preferred first adapter: ULinkRPC internal transport.
- Package shape: keep it outside `ULinkGame.Cluster` as `ULinkGame.Cluster.ULinkRPC`, so the core cluster contracts remain transport-neutral and do not force ULinkRPC server/client packages into every cluster consumer.
- ULinkRPC adapter scope: node-to-node `ClusterMessage` delivery, remote `IRouteDirectory` calls, adapter-owned authentication/TLS/compression/wire format, trace propagation, timeout mapping, and explicit `ClusterSendStatus` results.
- ULinkRPC adapter non-goals: durable route storage, service discovery, gameplay DTOs, durable queues, remote actor proxies, generated remote actor clients, or transparent local-looking actor references.
- Direction for gRPC: keep as the second candidate when a project needs a conventional service-to-service protocol or polyglot operations story.
- Direction for Redis pub/sub or streams: use only for fanout or brokered delivery after ordering, backpressure, expiry, and observability semantics are explicit; do not serialize live RPC callbacks or actor references into Redis.
- Direction for custom message buses: keep as an adapter pattern, not a framework default.

Implementation gates before making `ULinkGame.Cluster.ULinkRPC` a recommended generated-template option:

1. A sample or template needs two independent .NET processes exchanging `ClusterMessage`.
2. The adapter can authenticate node-to-node traffic without embedding production secrets in templates.
3. Timeout, expired message, route-not-found, handler-unavailable, backpressure, and failed-send paths remain mapped to `ClusterSendStatus`.
4. Metrics and activity spans preserve the current low-cardinality cluster tags.
5. The adapter does not change `IRouteDirectory`, `IClusterRouter`, `INodeMessenger`, or `IClusterMessageHandler` into transport-specific APIs.

### Prior Art And Direction

Skynet-derived cluster principles:

- Keep cluster support as infrastructure, not a full distributed actor platform. Provide node identity, route location, message delivery, diagnostics, and explicit failure results first.
- Make remote boundaries visible in API names and return types. Local calls and remote sends must not share the same surface.
- Treat overload as a normal result, not an exceptional surprise. Route lookup success does not guarantee delivery; node messenger, remote inbox, and target actor mailbox can all reject work with backpressure.
- Keep trace context in the message envelope. Cluster messages should carry correlation data that lets tools reconstruct route lookup, remote send, receive, local dispatch, and actor handling time.
- Do not use `ClusterMessage` as a generic large-state sync protocol. Large snapshots, fanout target sets, and repeated state should use application-owned versioning, caching, or diff protocols.

Skynet and ET framework comparison:

- Skynet cluster RPC is closest to explicit node-to-node message delivery. Callers address a node plus service name or address, the local cluster sender writes a request over TCP, the remote cluster agent dispatches to the target service, and call responses are correlated back to the waiting coroutine by session id. This is useful prior art for a future `INodeMessenger`: keep node messaging simple, expose send versus call semantics clearly, and do not pretend the cluster is a complete deployment, discovery, or failover system.
- ET's ActorLocation model is closer to location-transparent actor messaging. A stable entity id is resolved through a Location Server to a current actor instance id; senders cache the resolved location, retry after actor-not-found or migration failures, and use location locks during migration so concurrent sends can wait for a new location. This is useful prior art for route directory behavior: generation-aware locations, cache invalidation, bounded retry, and migration or rebinding locks.
- ULinkGame should borrow ET's location-directory mechanics without adopting ET's transparent remote actor surface. A future route location layer may support stable route keys, node epochs, location generations, stale-location detection, and bounded re-resolution after `RouteNotFound`, `HandlerUnavailable`, timeout, or state-moved results. The public API should still require callers to use explicit route or cluster APIs and handle remote failure outcomes.
- Do not add a generated remote actor client that has the same method shape as a process-local actor reference. If request/reply is added above `IClusterRouter`, name it as remote work, require timeout and expiration, and return a structured delivery or call result.
- Do not turn route lookup into a hidden global singleton. If the framework adds cached route senders, cache lifetime, invalidation, node epoch mismatch, and retry count must be visible in options and diagnostics.
- Do not make migration a default framework promise. ULinkGame may support rebinding route locations and rejecting stale generations; application-owned room transfer, snapshot handoff, and authoritative state repair remain game protocols unless repeated projects prove a reusable infrastructure shape.

### Cluster Management

The first cluster model should be simple and lease-based. Avoid starting with a fully decentralized consensus system.

The route directory should treat locations as expiring records. When a node dies or stops heartbeating, its temporary route locations should expire or be cleared. A stale location is a normal distributed condition, not a fatal error.

Recommended lifecycle:

1. Node starts and reads cluster name, node id, capabilities, endpoints, and labels.
2. Node registers with the route or node directory.
3. Directory assigns or records a node epoch and lease.
4. Node heartbeats until it drains or dies.
5. During draining, node stops accepting new ownership but may finish existing work.
6. Expired leases make affected routes unavailable until another node registers a new location.

Shutdown should use explicit draining rather than destructor-style cleanup. A draining node should stop accepting new route ownership, reject or redirect new remote sends, finish only bounded in-flight work, close external connections, flush required state, and then let process shutdown terminate anything still unfinished. The framework should not promise that every pending distributed request naturally completes during shutdown.

### Deferred Cluster Capabilities

Do not implement these as default framework behavior in the first cluster module:

- automatic actor migration
- transparent remote actor calls
- distributed transactions
- exactly-once cross-node delivery
- battle live migration
- fully decentralized route directory
- Raft-based cluster consensus
- cross-node shared mutable objects

## Next Development Plan

This plan tracks framework-level work only. Keep completed milestones out of this section, and do not use it for sample gameplay, account systems, matchmaking policy, room rules, leaderboard rules, Unity UI, persistence schema, or other game-owned work.

The next framework milestone is to move `ULinkGame.Cluster` from in-memory contract validation toward multi-physical-machine readiness while preserving explicit remote boundaries. The cluster plan borrows Skynet's simple node-to-node RPC shape and ET's location-directory mechanics, but does not adopt transparent remote actor references.

ULinkGame must not depend on ULinkActor scheduler internals. Any cluster-to-actor bridge should call public actor runtime APIs only.

There is no active implementation milestone in this section after the current cluster-readiness pass. Add a new milestone only when a consuming sample, generated template, or production adapter requirement proves the next reusable framework boundary.

### Deliberately Not Default Work

- Durable reliable push is not a default framework goal. The default remains an in-memory short-window outbox plus explicit state-lost/new-session behavior when authoritative state cannot be validated.
- `ULinkGame.Shared` is not planned. Cross-side framework-owned contracts belong in `ULinkGame.Abstractions`; user-owned business DTOs belong in the game's own shared contract project.
- `ULinkGame.Unity` is not planned until repeated Unity-specific integration code becomes stable enough to justify a package.
- Game business systems stay out of the framework: accounts, matchmaking policy, room rules, gameplay simulation, rewards, inventory, leaderboard rules, UI, and product DTOs belong in the game project or sample.
