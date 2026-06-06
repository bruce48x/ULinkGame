# Server Default Entrypoint Design

> **Historical note:** This design predates the current configuration/startup model. For canonical guidance, use [ULinkGame Configuration And Startup Model](../../ulinkgame-configuration-startup.md), which replaces singular `ULinkGame:Endpoint` guidance with `ULinkGame:Endpoints[]` and Feature Catalog startup.

## Purpose

Generated ULinkGame server projects should teach the user's game shape first and the framework assembly mechanics later. The current generated `Program.cs` runs, but it exposes too much internal setup: runtime option derivation, RPC server options, cluster defaults, hotfix loading, check output, and service registration all compete for attention in the first file a new user opens.

Because ULinkGame is still in development and has no external compatibility burden, the default server entrypoint should be redesigned around a long-lived application model instead of preserving the current generated code shape.

## Long-Term Direction

`Program.cs` should permanently remain a tiny intent file. It should say, in code, "create a ULinkGame server application and run this game." It should not explain how cluster, hotfix, reliable push, runtime validation, or ULinkRPC hosting are wired.

The target generated entrypoint is:

```csharp
using ULinkGame.Server.Hosting;

var builder = ULinkGameServerApplication.CreateBuilder(args);

builder.AddGameServer<GatewayGame>();

await builder.RunAsync();
```

An equivalent .NET Host-style API is acceptable if it stays equally small:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddULinkGameApplication(builder.Configuration, app =>
{
    app.UseGeneratedDefaults();
    app.AddGameServer<GatewayGame>();
});

await builder.Build().RunAsync();
```

The first form is preferred because it gives the framework one clear default application surface and makes the generated project read like a game server, not a dependency injection sample.

## Design Principles

1. `Program.cs` expresses intent, not mechanism.
2. Defaults live in framework APIs, not copied template code.
3. Generated projects expose business editing points before infrastructure editing points.
4. Cluster, hotfix, and reliable push are part of the default ULinkGame application model.
5. Advanced configuration is available through explicit override points, not through noisy default files.

## Generated Project Shape

The generated server project should guide the user toward business code:

```txt
Server/
  Program.cs
  Game/
    GatewayGame.cs
    ChatService.cs
    GameNotificationService.cs
    GameRulesService.cs
  Hosting/
    Advanced/
      ULinkGameGeneratedApplication.cs
      DefaultRpcServerConfigurator.cs
  Hotfix/
    ChatRules.cs
```

`Program.cs` is the first-read file. It should stay small enough to understand without knowing ULinkRPC, ULinkActor, cluster routing, hotfix loading, or reliable push internals.

`Game/` is the first-edit area. It owns the generated vertical slice: login, session binding, welcome notification, reconnect behavior, and hotfix rule invocation.

`Hosting/Advanced/` is an implementation-detail escape hatch during the transition. Users can inspect it when they need to understand or override generated conventions, but the default docs should not send new users there first.

Eventually, `Hosting/Advanced/ULinkGameGeneratedApplication.cs` should disappear from generated projects as its behavior moves into `ULinkGame.Server`.

## Framework Application Model

`ULinkGame.Server` should provide a default application model that derives the same runtime state currently generated into projects:

- local single-node cluster topology
- gateway endpoints from `ULinkGame:Endpoints[]`
- node identity from `ULinkGame:Node:Id`
- node-directory and route-directory services
- reliable push defaults
- hotfix source from project conventions
- runtime validation rules
- `--ulinkgame-check` output
- ULinkRPC server hosting

The generated project config should remain small:

```json
{
  "ULinkGame": {
    "Node": {
      "Id": "dev-1"
    },
    "Endpoints": [
      {
        "Transport": "kcp",
        "Host": "127.0.0.1",
        "Port": 20000
      }
    ]
  }
}
```

This config contains source values the user may reasonably change. Derived values should be shown through check output, not copied into `appsettings.json`.

## Override Model

Advanced users should override defaults through named, typed options:

```csharp
builder.AddGameServer<GatewayGame>(options =>
{
    options.Endpoint.Port = 20000;
    options.Hotfix.ProjectPath = "../Hotfix/Server.Hotfix.csproj";
});
```

The override surface should stay small and high-level. It must not require ordinary users to construct cluster bootstrap graphs, ULinkRPC configurator types, or hotfix assembly sources by hand.

Lower-level APIs may remain available for infrastructure projects and tests, but they should not appear in generated defaults.

## Migration Plan

### Phase 1: Thin Generated Entrypoint

Change the tool template so generated `Program.cs` delegates to a generated application helper. Move current setup logic into `Server/Hosting/Advanced/ULinkGameGeneratedApplication.cs`.

This phase is a project-shape improvement with low runtime risk. The generated server should still produce the same behavior: run, check, hotfix-load, host RPC, expose cluster defaults, and register reliable push.

### Phase 2: Introduce Framework Application API

Move the generated helper shape into `ULinkGame.Server` as a supported API. The API should own:

- builder creation
- default configuration loading
- check command dispatch
- derived runtime options
- default service registration
- initial hotfix loading
- host run lifecycle

The generated helper can become a small adapter around the framework API during this phase.

### Phase 3: Remove Copied Framework Assembly Code

Once the framework API is stable, generated projects should stop copying default runtime assembly code. The template should generate only:

- tiny `Program.cs`
- game declaration type
- business vertical slice
- small config
- task-oriented docs

This makes old generated project maintenance easier because framework default behavior can improve through package upgrades instead of template rewrites.

## Testing Strategy

Template tests should assert the generated `Program.cs` remains small and does not contain framework mechanism types such as:

- `ClusterOptions`
- `ServerRpcServerOptions`
- `CurrentDirectoryHotfixAssemblySource`
- `AddULinkRpcServer`
- `ULinkGameRuntimeOptions`

Functional tests should scaffold a project, build it, run `--ulinkgame-check`, and start the server long enough to verify host construction.

Framework API tests should validate that the application model derives the same runtime state as the current template:

- node id
- endpoints from `ULinkGame:Endpoints[]`
- advertised client endpoint
- local cluster services
- hotfix assembly path
- reliable push defaults
- check command text and JSON output

## Documentation Impact

Generated docs should direct users in this order:

1. run the server
2. run `--ulinkgame-check`
3. edit the first RPC or game service
4. edit the first reliable push notification
5. edit the first hotfix rule
6. inspect advanced hosting only when changing deployment behavior

Package README files should describe the framework application API. Generated project docs should explain where to edit game code and how to inspect derived runtime state.

## Success Criteria

A new user can open the generated project and understand where to write game logic without reading framework hosting internals.

`Program.cs` stays small across future features. New default capabilities must be added behind the ULinkGame application API or explicit game declarations, not by expanding the entrypoint.

Generated projects upgrade better over time because the default application model lives in package code instead of duplicated template code.
