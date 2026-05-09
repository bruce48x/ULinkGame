# Changelog

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
- Updated `ULinkGame.Tool` project scaffolding to expose the generated client-facing server as `Server/Edge/Edge.csproj` instead of `Server/Server/Server.csproj`, while keeping `Server/Silo/Silo.csproj` for Orleans grains.

## 2026-05-08

### Released

- `ULinkGame.Tool` `0.1.10`

### Changed

- Updated generated local `ulinkrpc.starter` tool manifests to use `0.2.53`.
- Suppressed delegated `ulinkrpc-starter` next-step output during `ulinkgame-tool new` so the command only prints the final ULinkGame next steps.
