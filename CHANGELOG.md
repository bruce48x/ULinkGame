# Changelog

## 2026-06-02

### Released

- `ULinkGame.Server.Hotfix.Abstractions` `0.1.1`
- `ULinkGame.Server.Hotfix` `0.1.1`
- `ULinkGame.Cluster` `0.1.4`
- `ULinkGame.Server` `0.1.16`
- `ULinkGame.Server` `0.1.17`
- `ULinkGame.Server` `0.1.18`
- `ULinkGame.Server` `0.1.19`
- `ULinkGame.Server` `0.1.20`
- `ULinkGame.Server` `0.1.21`
- `ULinkGame.Server.Generators` `0.1.1`
- `ULinkGame.Server.Generators` `0.1.2`
- `ULinkGame.Server.Generators` `0.1.3`
- `ULinkGame.Server.Generators` `0.1.4`
- `ULinkGame.Tool` `0.3.4`
- `ULinkGame.Tool` `0.4.1`
- `ULinkGame.Tool` `0.4.2`
- `ULinkGame.Tool` `0.4.3`
- `ULinkGame.Tool` `0.4.4`
- `ULinkGame.Tool` `0.4.5`

### Added

- Added actor call exception types and a remote actor call helper for generated actor APIs.
- Added cluster feature node discovery APIs for listing or selecting ready nodes by service feature without exposing endpoints.
- Added server-side actor directory contracts and an in-memory actor directory implementation.
- Added an in-memory actor directory cache for actor id to node id lookups.
- Added a feature-discovery based actor directory client abstraction that caches the directory host node and rediscovers once after host failure.
- Added generated distributed `Get(id)` actor accessors that resolve local-first, then actor directory cache/directory, before remote actor invocation.
- Added actor lifecycle hook attributes and a local actor node identity service for generated managed actor lifecycle.
- Added generated local-only `SpawnAsync` and `DestroyAsync` actor lifecycle APIs with actor directory registration, cache updates, and rollback on spawn failure.

### Changed

- Replaced hardcoded hotfix assembly path with `Path.Combine(AppContext.BaseDirectory, "hotfix")` in generated server code, and added an MSBuild target to copy `Server.Hotfix.dll` into the server output directory after each build. The path is now configuration-independent (works in Debug/Release, any target framework).
- Updated generated remote actor methods to throw actor call exceptions on remote failure instead of emitting status checks and constructing `RemoteActorException` inline.
- Updated generated actor lifecycle ordering to claim actor directory ownership before spawn hooks and unregister ownership before destroy hooks.

### Fixed

- Fixed lifecycle hook diagnostics so spawn hooks may take a request and destroy hooks may not.
- Fixed `ulinkgame-tool new` chat templates to emit C# 9-compatible block-scoped namespaces instead of file-scoped namespaces for Unity-created projects.
- Fixed `ulinkgame-tool new` Unity chat templates to use the generated `Rpc.Generated.RpcClient` API and emit the missing task namespace import.
- Fixed `ulinkgame-tool new` server templates to use the ULinkRPC callback-service constructor shape and copy the generated hotfix assembly into the server runtime output.
- Fixed `ulinkgame-tool new` Unity chat UI templates to avoid null-conditional event subscription syntax on `Button.clicked`.
- Fixed `ulinkgame-tool new` Unity chat projects to install the UI Toolkit chat document into the starter scene.

## 2026-06-01

### Released

- `ULinkGame.Server` `0.1.12`
- `ULinkGame.Server` `0.1.13`
- `ULinkGame.Server` `0.1.14`
- `ULinkGame.Server` `0.1.15`
- `ULinkGame.Server.Generators` `0.1.0`

### Added

- Added typed actor metadata primitives for the server actor runtime API.
- Added server-side typed actor source generation for `Actor<TKey>` local/remote accessors, cluster handlers, and service registration.
- Added `ULinkGame.Server.Generators` analyzer references to generated server projects.

### Fixed

- Fixed `RemoteActorInvoker` pending-reply cleanup on send failure and direct-node delivery for remote actor invocations.
- Fixed `RemoteActorInvoker.AskAsync` pending-reply cleanup when direct node send throws.
- Fixed `RemoteActorInvoker.AskAsync` cancellation mapping during direct node send.

## 2026-05-31

### Released

- `ULinkGame.Server` `0.1.10`
- `ULinkGame.Tool` `0.2.13`
- `ULinkGame.Tool` `0.3.1`

### Added

- Added runtime guardrail diagnostics, a resolved runtime model, and initial validation rules for node id, endpoints, hotfix presence, and duplicate cluster services.
- Updated generated `--ulinkgame-check` to reuse runtime guardrail diagnostics and support `--json` output.

### Fixed

- Updated Unity/Tuanjie scaffolding to pin `ULinkGame.Abstractions` beside `ULinkGame.Client` in `Assets/packages.config`.
- Added a generated Unity editor import guard that disables NuGet analyzer DLLs under `Assets/Packages/**/analyzers/` so Unity does not load Roslyn analyzers as runtime plugins.

## 2026-05-30

### Released

- `ULinkGame.Server` `0.1.9`
- `ULinkGame.Tool` `0.2.12`

### Changed

- Replaced hardcoded `ULinkActor` project reference with NuGet package `ULinkActor` `0.3.0` in `ULinkGame.Server`.

## 2026-05-29

### Released

- `ULinkGame.Tool` `0.2.10`
- `ULinkGame.Tool` `0.2.11`

### Added

- Added server hotfix design and initial runtime/generator packages for attribute-discovered hotfix systems.
- Added Agar sample gameplay-rule hotfix integration for arena tick and settlement behavior.
- Added default `ULinkGame.Tool` hotfix scaffolding with stable `Shared` state, a separate `Server.Hotfix` assembly, hotfix package references, runtime loading, and boundary examples.

## 2026-05-28

### Released

- `ULinkGame.Cluster` `0.1.3`
- `ULinkGame.Cluster.Sql` `0.1.0`
- `ULinkGame.Cluster.ULinkRPC` `0.1.2`
- `ULinkGame.Tool` `0.2.8`
- `ULinkGame.Tool` `0.2.9`

### Added

- Added node-directory contracts, in-memory and SQL-backed storage, ULinkRPC node-directory adapter, and node-local service configuration scaffolding for cluster deployments.

### Changed

- Updated `ULinkGame.Tool` to consume `ULinkRPC.Starter` `0.3.4`.

## 2026-05-27

### Released

- `ULinkGame.Server` `0.1.8`
- `ULinkGame.Tool` `0.2.7`

### Added

- Added ULinkGame-owned actor diagnostics for ULinkActor dead letters, slow messages, and call timeouts through `ActorRuntimeOptions`.
- Added explicit local actor backpressure with `IActorRuntime.TryTell(...)` and `ActorTellResult`.
- Added `ClusterActorTellDispatcher<TActor>` for one-way cluster actor dispatch that maps local mailbox pressure to `ClusterSendStatus.Backpressure`.
- Added explicit actor stop/drain APIs with `ActorStopOutcome`.
- Added ULinkGame-owned mailbox metrics through `IActorRuntime.TryGetMailboxMetrics(...)`.
- Added mailbox-native timer registration through the actor runtime facade so timer ticks enter the actor mailbox.
- Added `Actor.OnDeactivateAsync(...)` for explicit cleanup during actor stop.

### Changed

- Documented the ULinkActor facade design principles in `CONTRIBUTING.md`.
- Updated `ULinkGame.Server` actor documentation to show the ULinkGame facade as the recommended API while keeping ULinkActor native APIs as an opt-in lower-level choice.
- Updated `ULinkGame.Tool` so generated project templates consume `ULinkGame.Server` `0.1.8`.

## 2026-05-26

### Released

- `ULinkGame.Abstractions` `0.1.2`
- `ULinkGame.Cluster` `0.1.0`
- `ULinkGame.Cluster` `0.1.1`
- `ULinkGame.Cluster` `0.1.2`
- `ULinkGame.Cluster.ULinkRPC` `0.1.0`
- `ULinkGame.Cluster.ULinkRPC` `0.1.1`
- `ULinkGame.Server` `0.1.6`
- `ULinkGame.Server` `0.1.7`
- `ULinkGame.Tool` `0.2.2`
- `ULinkGame.Tool` `0.2.3`
- `ULinkGame.Tool` `0.2.4`
- `ULinkGame.Tool` `0.2.5`
- `ULinkGame.Tool` `0.2.6`

### Added

- Added the initial `ULinkGame.Cluster` package with explicit node/route/message contracts, actor route envelopes, in-memory route directory, loopback messenger, router diagnostics, and unit tests.
- Added route generation, node epoch, stale route registration rejection, conditional lease refresh, node-epoch clearing, and explicit stale route status values to `ULinkGame.Cluster`.
- Added the initial `ULinkGame.Cluster.ULinkRPC` adapter package with a ULinkRPC cluster send method descriptor, node messenger, client factory, transport factory boundary, TCP transport factory, server binder, and unit tests.
- Added a TCP smoke test proving that `ULinkGame.Cluster.ULinkRPC` can send a `ClusterMessage` through a ULinkRPC server binder.
- Added `ULinkRpcRouteDirectory` and `ULinkRpcRouteDirectoryBinder` so route register, resolve, expiration, lease refresh, clear-by-node, and clear-by-node-epoch can run through a ULinkRPC-managed route directory service.
- Added a TCP smoke test proving the ULinkRPC route directory adapter preserves route generation and node epoch semantics across the transport.
- Added `samples/Cluster.TwoNode`, a cross-process ULinkRPC cluster smoke sample that starts separate route-directory and worker processes and verifies local dispatch, remote dispatch, route-not-found, expiration, timeout, handler-unavailable, backpressure, stale registration rejection, node-epoch clearing, and node restart.
- Added explicit `ulinkgame-tool new --network-profile cluster` scaffolding for cluster package references, environment-variable-friendly cluster node, endpoint, lease, send-timeout settings, and a local `--health-check` configuration probe.
- Added explicit `ulinkgame-tool new --deploy-profile compose` scaffolding for local cluster deployment rehearsal with Dockerfile, compose healthcheck, `.env.cluster.example`, and an operations note that avoids production secrets.
- Added `ULinkRpcClusterDependencyProbe` so hosts can check ULinkRPC route-directory dependency health with bounded timeout and explicit healthy/timeout/unhealthy results.
- Added `ClusterActorDispatcher<TActor>` in `ULinkGame.Server` to adapt explicit cluster actor envelopes into the local `IActorRuntime` mailbox without exposing transparent remote actor references.
- Added a minimal `samples/Cluster.Loopback` sample that demonstrates in-memory local dispatch, remote loopback dispatch, route-not-found, expiration, timeout, and backpressure.

### Changed

- Updated `ULinkRpcClusterClientFactory` so client cache reuse is scoped by node epoch and endpoint address, preventing a restarted node with the same `NodeId` from inheriting a stale connection.
- Updated `ULinkGame.Server` to consume `ULinkActor` `0.2.0` while preserving the existing process-local `IActorRuntime` facade.
- Updated `ULinkGame.Tool` so generated project templates consume `ULinkGame.Server` `0.1.7`.
- Updated `ulinkgame-tool new` to automatically install the pinned `ULinkRPC.Starter` tool when `ulinkrpc-starter` is not already available.
- Updated `ulinkgame-tool new` so cluster scaffolding is generated by default and the `--network-profile` argument is no longer required.
- Updated generated `ulinkgame-tool` output to preserve the `ulinkrpc-starter` server project naming under `Server/Server/Server.csproj`, including namespace, Docker, compose, and health-check commands.
- Updated the cluster loopback sample to register generation-aware route locations.
- Reorganized `src/ULinkGame.Abstractions`, `src/ULinkGame.Cluster`, `src/ULinkGame.Cluster.ULinkRPC`, and `src/ULinkGame.Tool` source files into responsibility-focused directories without changing public namespaces or APIs.
- Reorganized `CONTRIBUTING.md` around repository workflow, package boundaries, runtime architecture, cluster architecture, and the current development plan.
- Documented the production cluster adapter decision: ULinkRPC is the first adapter direction, implemented as a separate transport package only after a real cross-process consumer exists.
- Removed completed or external cluster planning tasks from `CONTRIBUTING.md`; the next implementation should start only when the production adapter gates are met.

## 2026-05-25

### Released

- `ULinkGame.Tool` `0.2.1`

### Changed

- Updated `ULinkGame.Tool` to consume `ULinkRPC.Starter` `0.3.1`, remove the manual `codegen` command path, and keep generated projects on the `ULinkRPC.Analyzers` source-generator workflow.
- Added Simplified Chinese and Traditional Chinese CLI text for `ULinkGame.Tool`, matching the culture detection used by `ULinkRPC.Starter`.
- Migrated the Unity and Godot samples away from committed RPC `Generated/` sources; server and client RPC glue is now compiler output.

## 2026-05-21

### Changed

- Updated the ULinkActor integration to consume `ULinkActor` `0.1.9`.

## 2026-05-20

### Changed

- Documented the package boundary after publishing `ULinkActor` and `ULinkActor.SourceGenerator` as standalone NuGet packages.
- Clarified that `ULinkActor` is the actor/mailbox runtime foundation for ULinkGame; `ULinkGame.Server` builds on it for game-session infrastructure, ULinkRPC hosting, endpoint binding, reconnect, and reliable push integration.

## 2026-05-13

### Changed

- Clarified cluster routing documentation so `realtime` remains an optional template/sample profile instead of a framework-wide concept.
- Added cluster node-to-node communication design notes for route lookup, local dispatch, remote dispatch, and pluggable node messenger adapters.
- Added Skynet-derived cluster design principles for explicit remote boundaries, overload results, trace propagation, large-message boundaries, and draining shutdown.
- Merged the standalone architecture draft into `CONTRIBUTING.md` and removed duplicate repository design notes.

## 2026-05-12

### Released

- `ULinkGame.Abstractions` `0.1.1`
- `ULinkGame.Client` `0.1.5`
- `ULinkGame.Server` `0.1.5`
- `ULinkGame.Tool` `0.1.15`
- `ULinkGame.Tool` `0.1.16`
- `ULinkGame.Tool` `0.1.17`

### Changed

- Added `ULinkGame.Abstractions` for cross-side framework-owned session, endpoint, reconnect, and reliable push primitives.
- Added `IULinkGameServer` / `AddULinkGameServer()` and `ULinkGameClient` as the recommended single-entry APIs for server and client code.
- Added typed reliable push overloads on `IULinkGameServer` so recommended server code can deliver through endpoint callbacks without handling `ReliablePushRecord`.
- Moved shared `GameSessionKey`, `GameEndpointName`, `ReliablePushSequence`, reliable push acknowledgement outcomes, and session resume outcomes out of server/client-only namespaces.
- Changed `ULinkGame.Tool` to generate its ULinkGame runtime package version constants from the Server and Client project versions during build.
- Changed `ULinkGame.Tool` project templates to default to one RPC endpoint and require `--network-profile realtime` for separate control and realtime endpoints.
- Changed `ULinkGame.Tool` project initialization to add `ULinkGame.Client` to generated Unity and Godot client projects.
- Added `ULinkGame.Tool new --persistence none|mysql|postgres`; MySQL/PostgreSQL profiles add Dapper plus the selected database provider package to generated server projects.

## 2026-05-11

### Released

- `ULinkGame.Client` `0.1.4`
- `ULinkGame.Server` `0.1.4`
- `ULinkGame.Tool` `0.1.14`

### Changed

- Added framework session lifecycle primitives, reconnect/state-lost outcomes, session-scoped reliable push acknowledgement helpers, and engine-neutral client session state helpers.
- Migrated Unity and Godot samples to `ReliablePushInbox`.
- Updated `ULinkGame.Tool` package version constants for generated projects.

## 2026-05-09

### Released

- `ULinkGame.Tool` `0.1.11`
- `ULinkGame.Tool` `0.1.12`
- `ULinkGame.Tool` `0.1.13`

### Changed

- Updated generated local `ulinkrpc.starter` tool manifests and Godot verification to use `0.2.57`.
- Updated generated local `ulinkrpc.starter` tool manifests and Godot verification to use `0.2.58`.
- Preserved `ULinkRPC.*` package references from starter-generated projects instead of rewriting their versions in ULinkGame templates.
- Documented the `ulinkrpc-starter` ownership boundary for ULinkGame contributors.

## 2026-05-08

### Released

- `ULinkGame.Tool` `0.1.10`

### Changed

- Updated generated local `ulinkrpc.starter` tool manifests to use `0.2.53`.
- Suppressed delegated `ulinkrpc-starter` next-step output during `ulinkgame-tool new` so the command only prints the final ULinkGame next steps.

## 2026-05-07

### Released

- `ULinkGame.Client` `0.1.3`
- `ULinkGame.Tool` `0.1.7`
- `ULinkGame.Tool` `0.1.8`
- `ULinkGame.Tool` `0.1.9`

### Changed

- Removed Unity package metadata from `ULinkGame.Client`; it is now consumed as a NuGet package only, matching the `ULinkRPC.Client` layout.
- Updated Unity and Godot samples to consume `ULinkGame.Client` through NuGet.
- Updated Godot sample projects and generated tool templates to avoid MSBuild multi-target project races during default restore/build.
- Limited Godot server logging to console output to avoid Windows EventLog permission failures in non-elevated runs.
- Updated Godot client generation in `ULinkGame.Tool` to preserve generated RPC clients and create a real networked Ping example.
- Updated `ULinkGame.Tool` project scaffolding to expose the generated client-facing server as `Server/Server/Server.csproj`, while keeping the then-current `Server/Silo/Silo.csproj` state-process layout.
