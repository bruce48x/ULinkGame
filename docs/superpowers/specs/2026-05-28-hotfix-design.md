# ULinkGame Hotfix Design

Date: 2026-05-28

## Goal

Add first-class server hotfix support to ULinkGame so game projects can replace selected business logic assemblies at runtime without rebuilding long-lived in-memory state.

The first implementation targets framework support plus a concrete Agar Unity sample integration. It follows the project boundary already documented in `CONTRIBUTING.md`:

```text
stable runtime state + replaceable business logic
```

The initial sample focus is Agar gameplay rules, not login, matchmaking, or transport behavior.

## Scope

The first version supports:

- Server-side .NET hotfix through collectible `AssemblyLoadContext`.
- Stable state objects that remain alive across reload.
- Hotfix system methods discovered from attributes.
- Source-generated friend accessors for selected private fields on stable state types.
- Explicit stable wrapper methods at hotfix entry points. Full source-generated extension wrappers are staged after the first runtime integration.
- Manual reload through `IHotfixManager.ReloadAsync()`.
- Optional file-watch reload for development and operational convenience.
- Current-directory and version-pointer hotfix assembly sources.
- Reload failure behavior that keeps the previous logic active.
- Agar sample gameplay-rule hotfix for `ArenaSimulation` tick and match settlement rules.

The first version does not support:

- Client HybridCLR hotfix.
- Actor runtime, session runtime, reliable push, serializer, transport, or scheduler hotfix.
- State structure migration.
- Persistent schema migration.
- Automatic cross-node reload coordination.
- Multiple independent hotfix domains inside one server process.
- Transparent remote actors or generated remote actor clients.
- A general ET-style message, event, and system framework.
- Reflection-based private-field access in the gameplay tick hot path.

## Mental Model

Hotfix code is behavior, not ownership.

```text
Actor / runtime host
  Owns execution, mailbox or loop scheduling, cancellation, I/O, persistence, and side effects.

Stable state object
  Owns long-lived mutable state such as room simulation data, player records, food records, counters, and timers.

Hotfix system
  Owns replaceable logic that operates on the stable state object.
```

For the Agar sample, `RoomRuntime` remains the stable loop owner, `ArenaSimulation` remains the stable state object, and `ArenaSimulationSystem` is the reloadable rules implementation. `ArenaSimulationSystem` is not an actor. If a future room implementation becomes a ULinkGame actor, the actor remains stable and calls hotfix system methods on its stable state.

Hotfix system methods should not own long-lived state, subscribe to static events, start background timers, or hold callback references. Those patterns make unload unreliable and split state across old and new assemblies.

## Framework Packages

The hotfix work should be split so stable models and hotfix projects can reference only the minimum required contracts.

### `ULinkGame.Server.Hotfix.Abstractions`

Owns compile-time and shared runtime contracts:

- `HotfixStateAttribute`
- `HotfixSystemOfAttribute`
- `FriendOfAttribute`
- `HotfixMethodKey`
- `HotfixReloadStatus`
- `HotfixReloadResult`
- `HotfixSnapshot`

This package must remain small. It should not depend on ULinkRPC, ULinkActor, hosting, transport, sessions, or gameplay sample types.

### `ULinkGame.Server.Hotfix`

Owns server runtime hotfix infrastructure:

- `IHotfixManager`
- `IHotfixAssemblySource`
- current-directory assembly source
- version-pointer assembly source
- optional file watcher hosted service
- collectible `AssemblyLoadContext` loading
- dependency resolution
- attribute scanning
- dispatch table construction
- atomic dispatch table replacement
- reload diagnostics and unload diagnostics

This package can depend on `Microsoft.Extensions.Hosting.Abstractions` and the hotfix abstractions package.

### `ULinkGame.Server.Hotfix.Generators`

Owns source generation and analyzer-style diagnostics:

- friend accessor generation for stable state private fields
- diagnostics for invalid state types and inaccessible field types

Full extension wrapper generation for hotfix system methods and call-site cache generation remain a staged follow-up. The first integrated sample uses explicit stable wrapper methods that call `HotfixDispatch.Invoke(...)`.

The generator should be packaged as an analyzer/source-generator package and referenced by stable projects that declare hotfix states.

## Attribute Model

Stable state types opt into hotfix generation:

```csharp
[HotfixState]
public partial class ArenaSimulation
{
    private readonly Dictionary<string, ArenaPlayer> _players = new(StringComparer.Ordinal);
    private readonly List<ArenaFood> _foods = new();
}
```

Hotfix assemblies define static system classes:

```csharp
[FriendOf(typeof(ArenaSimulation))]
[HotfixSystemOf(typeof(ArenaSimulation))]
public static class ArenaSimulationSystem
{
    public static ArenaStepResult Tick(this ArenaSimulation self, float deltaTime)
    {
        // Replaceable gameplay rule logic.
    }

    public static MatchSettlementResult SettleMatch(this ArenaSimulation self, WorldState worldState)
    {
        // Replaceable settlement rule logic.
    }
}
```

First-version method rules:

- Methods must be `public static`.
- The first parameter must be `this TState self`.
- `TState` must match the `HotfixSystemOfAttribute` state type.
- Method names and signatures form the dispatch key.
- Overloads are allowed only when the full parameter and return signature is unambiguous.
- The runtime scanner rejects duplicate keys.

## Source-Generated Wrappers

Future generator work should emit stable extension wrappers so existing code can call natural methods:

```csharp
var result = arenaSimulation.Tick(deltaTime);
var settlement = arenaSimulation.SettleMatch(result.WorldState);
```

Future generated wrapper shape:

```csharp
public static ArenaStepResult Tick(this ArenaSimulation self, float deltaTime)
{
    return HotfixDispatch.Current.Invoke<ArenaSimulation, ArenaStepResult>(
        "Tick",
        self,
        deltaTime);
}
```

The first runtime implementation should not use reflection in the tick path. Runtime scanning can use reflection to build the dispatch table, but stable wrappers call `HotfixDispatch.Invoke(...)`. A later generator should emit a call-site cache keyed by dispatch table version so normal execution avoids repeated method lookup. When reload swaps the table and increments the version, the next call resolves the new delegate and updates the cache.

## Friend Accessors

First-version hotfix must support private field access, but only through generated strong-typed accessors in the stable assembly.

The generator emits accessors inside the partial stable type:

```csharp
public partial class ArenaSimulation
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Dictionary<string, ArenaPlayer> __hotfix_players()
    {
        return _players;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public List<ArenaFood> __hotfix_foods()
    {
        return _foods;
    }
}
```

Rules:

- Accessors are generated in the same assembly and partial type as the private fields, so access is legal C# rather than reflection.
- Generated names use a reserved `__hotfix_` prefix and are hidden from normal IntelliSense when possible.
- Hotfix code should call accessors only when the system declares `[FriendOf(typeof(TState))]`.
- In the first implementation, generated accessors are public members because the hotfix assembly is separate from the stable assembly. `[FriendOf]` is metadata and convention, not an enforced CLR permission boundary.
- The accessed field type must be visible to the hotfix assembly. Agar's current private nested `ArenaPlayer` and `ArenaFood` types must move to stable hotfix-visible types.
- Field rename, removal, or type change is not a state-preserving hotfix. It requires stable assembly deployment and a migration or restart strategy.

The first implementation should avoid private reflection in gameplay rule execution. Reflection can remain available for diagnostics or scanner implementation.

## Runtime Loading

`IHotfixManager.ReloadAsync()` performs the reload.

Flow:

1. Resolve the desired hotfix version from `IHotfixAssemblySource`.
2. Create a new collectible `AssemblyLoadContext`.
3. Load the hotfix main assembly, optional PDB, and dependencies from the resolved source.
4. Scan for `[HotfixSystemOf]` system classes and extension methods.
5. Build a new immutable dispatch table.
6. Validate duplicate keys, unsupported signatures, missing required methods, and dependency failures.
7. Atomically replace the current dispatch table only after the new table is valid.
8. Mark the old dispatch table and old load context for unload.
9. Return a `HotfixReloadResult` with version, source path, status, diagnostics, and previous-version information.

Reload failure keeps the previous dispatch table active. Existing rooms continue to run old logic, and diagnostics record the failed path, exception, and validation errors.

The first implementation uses one process-global dispatch table. Treat a server process as a single hotfix domain unless a later design introduces named dispatch tables.

## Assembly Sources

The runtime supports two source layouts.

Development-friendly current directory:

```text
hotfix/current/
  Agar.Sample.Hotfix.dll
  Agar.Sample.Hotfix.pdb
  dependencies...
```

Production-recommended version pointer:

```text
hotfix/current.txt
hotfix/versions/2026.05.28.1/
  Agar.Sample.Hotfix.dll
  Agar.Sample.Hotfix.pdb
  dependencies...
hotfix/versions/2026.05.28.2/
  Agar.Sample.Hotfix.dll
  Agar.Sample.Hotfix.pdb
  dependencies...
```

`current.txt` points to the version directory. Reload reads the pointer and loads from that immutable directory. This supports rollback and avoids half-overwritten DLLs. The current-directory source remains useful for local development and ET-like workflows.

## Switching Semantics

Reload uses next-entry semantics.

- A currently executing hotfix delegate continues with the version it already resolved.
- After the dispatch table swap, the next generated wrapper invocation resolves the new version.
- `RoomRuntime` does not pause all rooms during reload.
- `RoomRuntime` and stable actors must not cache hotfix implementation instances across calls.

For Agar's 20 Hz room loop, each tick starts by calling `arenaSimulation.Tick(deltaTime)`. If reload succeeds during a tick, that tick finishes with the old delegate and the next tick uses the new delegate.

## Agar Integration

The sample hotfix work focuses on gameplay rules.

Stable types:

- `ArenaSimulation`
- `ArenaSimulationOptions`
- `ArenaPlayerRegistration`
- `ArenaPlayerSnapshot`
- `ArenaStepResult`
- `ArenaPlayer`
- `ArenaFood`
- `WorldState`
- `PlayerState`
- `PickupState`
- `MatchEnd`
- settlement result DTOs

`ArenaSimulation` should become a partial stable state type. It keeps construction, player registration, removal, input submission, snapshot creation, and clear/reset lifecycle APIs. Internal gameplay state remains in stable memory.

Hotfix project:

```text
samples/Agar.Unity/Server/Hotfix/
  Agar.Sample.Hotfix.csproj
  Gameplay/ArenaSimulationSystem.cs
  Gameplay/ArenaSettlementSystem.cs
```

Hotfix systems own replaceable rules:

- arena bounds update
- player movement calculation
- food collection
- player consumption
- bot input choice
- match lifecycle checks
- winner selection
- ranking and victory point awards

`RoomRuntime` remains stable. It owns tick scheduling, cancellation, publishing callbacks, persistence, session cleanup, user profile writes, leaderboard writes, and logging. Hotfix settlement logic returns a stable result DTO describing ranks, winner, points, and reason; `RoomRuntime` performs the side effects.

Example stable call sites:

```csharp
lock (_gate)
{
    result = _simulation.Tick((float)TickInterval.TotalSeconds);
}

var settlement = _simulation.SettleMatch(result.WorldState);
```

In the first Agar integration, `TickWithHotfix(...)` and `SettleMatch(...)` are explicit stable methods on `ArenaSimulation`; full generated wrappers are not required for the v1 runtime.

## Testing

Framework tests should cover:

- successful hotfix assembly load and system scan
- dispatch table construction from `[HotfixSystemOf]`
- explicit stable wrapper calling v1 logic
- reload to v2 with the same stable state object
- reload failure retaining v1 logic
- duplicate method key diagnostics
- invalid signature diagnostics
- missing dependency diagnostics
- old `AssemblyLoadContext` weak-reference unload checks
- call-site cache invalidation when table version changes

Generator tests should cover:

- wrapper generation for valid system methods, when the staged wrapper generator is added
- friend accessor generation for private fields
- diagnostics when a hotfix-visible accessor would expose an inaccessible field type
- diagnostics for non-partial `[HotfixState]` types
- diagnostics for invalid extension method signatures

Agar sample tests should cover:

- v1 settlement awarding first place 10 points
- v2 settlement awarding first place 20 points after reload
- existing `ArenaSimulation` state surviving reload
- reload failure keeping v1 settlement behavior
- reload during room tick not interrupting the current tick and applying on the next tick
- hotfix system reading or mutating stable private state through generated accessors

## Operational Diagnostics

`HotfixSnapshot` should expose:

- current version
- source kind
- source path
- loaded assembly names
- loaded at UTC
- dispatch table version
- available method keys
- previous version
- last reload status
- last reload failure message and exception type
- unload status for previous load contexts when known

The optional file watcher should debounce changes and call `ReloadAsync()`. It is useful for development, but production should prefer explicit reload through an admin command, management RPC, or operational control plane.

## Risks

Source generation plus runtime loading is more complex than a simple interface registry. Keep the first API surface narrow and focus on the Agar gameplay rule scenario before expanding to actor messages or event systems.

Collectible `AssemblyLoadContext` unload is easy to block. Static events, timers, background tasks, cached delegates, and long-lived hotfix objects can all retain the old context. Tests need weak-reference unload checks, and documentation must tell users not to keep hotfix objects alive.

Friend accessors intentionally expose stable private state to hotfix code. This is powerful but not a migration tool. If a field changes shape, that is a stable model change and must be handled through deployment, restart, or explicit migration.

The Agar sample currently uses `UnityEngine.Vector2` in shared gameplay code. The hotfix project and tests must either keep that dependency stable or move gameplay math to a framework-neutral vector type in a separate design.

## Open Follow-Up

The next design discussion should walk through a `PlayerActor` example to clarify how actors, stable state, and hotfix systems interact when the state owner is a ULinkGame actor rather than `RoomRuntime`.
