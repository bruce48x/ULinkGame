# ULinkGame

ULinkGame is a game-oriented framework layer built on top of [ULinkRPC](https://github.com/bruce48x/ulinkrpc).

ULinkRPC owns typed RPC, generated glue code, transports, serializers, and bidirectional callbacks. ULinkGame starts one level above that boundary: it provides reusable building blocks for online game sessions where a client and server must keep business state consistent across connection lifecycle changes, realtime transports, reconnects, and server push messages.

## Project Layout

```txt
src/
  ULinkGame.Server/       Server-side hosting, Orleans integration, reliable push outbox
  ULinkGame.Client/       Engine-neutral client helpers, currently reliable push tracking
  ULinkGame.Tool/         Project management tool entry point

samples/
  Agar.Unity/             Unity + .NET multiplayer sample
    docs/                 Sample gameplay design and development plan
    tests/                Sample gameplay and server policy tests
  Agar.Godot/             Godot .NET client sample

tests/
  tests.slnx              Framework test entry point
  ULinkGame.Client.Tests/ Client package unit tests
  ULinkGame.Server.Tests/ Server package unit tests

docs/
  RELIABLE_PUSH_DESIGN.md Cross-package reliable push design
```

Cross-package framework design documents live under root `docs/`. Package-specific design documents live with the package that owns them. For example:

- `docs/RELIABLE_PUSH_DESIGN.md`
- `docs/ULINKGAME_PACKAGE_DECISION.md`

## Packages

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
dotnet test tests/tests.slnx
```

Sample-specific tests live with their sample, for example `samples/Agar.Unity/tests/BusinessLogic.Tests`.

The Unity project may generate local `Library`, `Temp`, `obj`, and restored NuGet package folders. These are ignored and should not be committed.

## Design Boundary

ULinkGame should not become a full game business framework. Keep the boundary narrow:

- Framework: connection lifecycle, host integration, session infrastructure, reliable push mechanics, reusable client state helpers.
- Game project: accounts, matchmaking policy, room rules, gameplay simulation, UI, persistence schema, and product-specific DTOs.

When a capability is only useful to one sample, keep it under that sample in `samples/`. Move it into `src` only when it is demonstrably reusable across games.
