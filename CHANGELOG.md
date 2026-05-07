# Changelog

## 2026-05-07

### Released

- `ULinkGame.Client` `0.1.3`
- `ULinkGame.Tool` `0.1.7`
- `ULinkGame.Tool` `0.1.8`

### Changed

- Removed Unity package metadata from `ULinkGame.Client`; it is now consumed as a NuGet package only, matching the `ULinkRPC.Client` layout.
- Updated Unity and Godot samples to consume `ULinkGame.Client` through NuGet.
- Updated Godot sample projects and generated tool templates to avoid MSBuild multi-target project races during default restore/build.
- Limited Godot server logging to console output to avoid Windows EventLog permission failures in non-elevated runs.
- Updated Godot client generation in `ULinkGame.Tool` to preserve generated RPC clients and create a real networked Ping example.
