# Changelog

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
- Updated `ULinkGame.Tool` project scaffolding to expose the generated client-facing server as `Server/Edge/Edge.csproj` instead of `Server/Server/Server.csproj`, while keeping the then-current `Server/Silo/Silo.csproj` state-process layout.
