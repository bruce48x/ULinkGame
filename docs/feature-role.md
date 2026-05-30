# Feature & Role — Component-Based Server Assembly

`IFeature` and `INodeRole` implement the "N→1, 1→N" pattern: develop with every service in a single process, deploy with services split across processes — without changing a line of code.

## Concepts

| Concept | Responsibility |
|---------|---------------|
| `IFeature` | A pure capability unit. It has one method: `Configure(IServiceCollection, IConfiguration)`. It does not know about deployment, roles, or dependencies. |
| `INodeRole` | A deployment unit. It has a `Name` and an ordered list of `IFeature[]`. The array order determines `Configure()` invocation order (natural topological sort). |
| `FeatureBuilder` | Collects roles (manually or via assembly scanning), applies a `FeatureFilter`, and resolves the final deduplicated feature list. |
| `FeatureFilter` | Selects which roles to activate. `Roles = ["gateway"]` means only the gateway role's features are configured. |

## Design

Features are deduplicated by **type**. If `GatewayRole` and `RoomRole` both reference `ClusterFeature`, it's configured only once. This is correct: the same service registration should not happen twice.

Feature `Configure()` order follows the role's array. No declarative `DependsOn` — the array is the dependency order.

## Usage

### Define a Feature

```csharp
public sealed class KcpRealtimeFeature : IFeature
{
    public void Configure(IServiceCollection services, IConfiguration config)
    {
        services.AddULinkRpcServer<KcpRealtimeConfigurator>();
    }
}
```

### Define a Role

```csharp
public sealed class RoomRole : INodeRole
{
    public string Name => "room";

    public IFeature[] Features => [
        new ClusterFeature(),       // 1. cluster routing must come first
        new ActorRuntimeFeature(),  // 2. actor runtime
        new KcpRealtimeFeature(),   // 3. KCP transport
        new RoomFeature(),          // 4. room-specific services
    ];
}
```

### Wire in Program.cs

```csharp
// Manual registration
builder.Services.AddFeatures(builder.Configuration, features =>
{
    features.AddRole<GatewayRole>();
    features.AddRole<RoomRole>();
});

// Assembly scanning
builder.Services.AddFeatures(builder.Configuration, features =>
{
    features.FromAssembly(typeof(GatewayRole).Assembly);
});
```

### Run

```bash
# Development — all roles in one process
dotnet run

# Production — one role per process
dotnet run --ULinkGame:Features:Roles=gateway
dotnet run --ULinkGame:Features:Roles=room
```

### Filter from appsettings.json

```json
{
  "ULinkGame": {
    "Features": {
      "Roles": ["gateway"]
    }
  }
}
```

## Integration with existing APIs

`AddFeatures()` is additive. It coexists with `AddULinkGameServerActors()`, `AddULinkGameServerSessions()`, and all other DI extension methods. Features can call these internally or be used alongside them.

## Assembly scanning

`FromAssembly()` discovers all concrete `INodeRole` implementations in the given assembly. Roles must have parameterless constructors. Types without one are silently skipped.

## Error handling

If a requested role is not found, `ResolveFeatures()` throws `InvalidOperationException` listing both the missing roles and the available ones. This surfaces configuration errors at startup, not at runtime.
