# Changelog

## Unreleased

### Released

- `ULinkGame.Abstractions` `0.1.1`
- `ULinkGame.Client` `0.1.5`
- `ULinkGame.Server` `0.1.5`
- `ULinkGame.Tool` `0.1.16`
- `ULinkGame.Tool` `0.1.17`

### Changed

- Documented the package boundary after publishing `ULinkActor` and `ULinkActor.SourceGenerator` as standalone NuGet packages.
- Clarified that `ULinkActor` is the actor/mailbox runtime foundation for ULinkGame; `ULinkGame.Server` builds on it for game-session infrastructure, ULinkRPC hosting, endpoint binding, reconnect, and reliable push integration.
- Clarified that ULinkGame is not an Orleans wrapper; Orleans-style distributed actor hosting is too heavy for the targeted lightweight game-server execution model.
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

## 2026-05-08

### Released

- `ULinkGame.Tool` `0.1.10`

### Changed

- Updated generated local `ulinkrpc.starter` tool manifests to use `0.2.53`.
- Suppressed delegated `ulinkrpc-starter` next-step output during `ulinkgame-tool new` so the command only prints the final ULinkGame next steps.
