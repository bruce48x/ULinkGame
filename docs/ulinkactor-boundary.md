# ULinkActor / ULinkGame Boundary

## The Facade Pattern

ULinkGame.Server wraps ULinkActor behind a facade (`ULinkActorRuntime.cs` is the **only** file that references ULinkActor types directly). All other ULinkGame code goes through `IActorRuntime` and never sees ULinkActor internals.

This boundary is deliberate. It prevents process-local actor semantics from leaking into higher-level infrastructure, and allows ULinkActor to evolve independently.

## Responsibility Split

```
ULinkActor                          ULinkGame
─────────────────────────────       ─────────────────────────────
Actor identity (long ActorId)       Game identity (string ActorId)
Mailbox + serialization             Session management
Tell / Call (process-local)         Cluster routing (cross-node)
Timer dispatch                      Reliable push (at-least-once)
Lifecycle (start/stop/drain)        Hotfix (AssemblyLoadContext)
Diagnostics (Activity/Meter)        Gate / Watchdog / Agent patterns
Execution timeout                   Server hosting (DI, config)
Message interceptor hooks           Message recording/replay (storage)
Actor state reporting               Service discovery
```

## Feature Placement Rules

### Belongs in ULinkActor

A feature belongs in ULinkActor if it answers: **"How does a single actor execute safely?"**

Examples:
- Message dispatch with try-catch isolation
- Mailbox capacity and backpressure
- Timer scheduling and delivery
- Execution timeout (interrupt stuck handlers)
- Call chain tracking and deadlock detection
- Activity/span propagation through Tell/Call
- Message interception hooks (mechanism, not storage)
- ActorId monotonic generation
- Actor lifecycle state (Active → Draining → Dead)

### Belongs in ULinkGame

A feature belongs in ULinkGame if it answers: **"How do multiple nodes cooperate?"** or **"How does a game server compose its services?"**

Currently implemented:
- Cluster routing and node directory
- Session resume and token validation
- Reliable push outbox/inbox
- Hotfix assembly loading and dispatch table swap
- Component-based server assembly (`IFeature` / `INodeRole`)
- Remote actor messaging (typed `Local(id)` / `Remote(nodeId, id)` refs over lower-level `AskRemoteAsync` / `TellRemoteAsync` plumbing)
- Message recording storage and replay (`IMessageLogStore`)
- Game-specific ActorId scheme (string with generation)

Potentially belongs here in the future:
- Cross-server event bus (currently: Redis pub-sub recommended)
- Service discovery and leader election (currently: static config + INodeDirectory)

### Belongs in a shared Analyzer

Analyzer rules apply across the boundary:

| Rule | Scope |
|------|-------|
| ULA001 (no self-call) | ULinkActor |
| ULA002 (no blocking wait) | ULinkActor |
| ULA003 (no discarded call) | ULinkActor |
| Actor isolation rules | Shared (future) |
| Thread safety annotations | Shared (future) |

## Configuration Flow

```
ULinkGame.ActorRuntimeOptions
    └─ maps to → ULinkActor.ActorSystemOptions
        ├─ MailboxCapacity
        ├─ SlowMessageThreshold
        ├─ ExecutionTimeout       ← new in 0.3.0
        └─ MessageInterceptor     ← new in 0.3.0
    └─ maps to → ULinkActor.ActorSpawnOptions
        └─ MailboxCapacity
```

ULinkGame adds its own configuration on top:
- `CallTimeout` (for AskAsync)
- Diagnostic event handlers (DeadLetter, SlowMessage, CallTimeout)

## When ULinkActor changes, ULinkGame adapts

| ULinkActor change | ULinkGame adaptation |
|------------------|---------------------|
| New config option | Expose via `ActorRuntimeOptions` |
| New public API | Wrap in `IActorRuntime` if relevant |
| New diagnostic event | Forward through ULinkGame handler |
| Breaking change | Update facade mapping in `ULinkActorRuntime.cs` |
| New interceptor hook | Implement `IActorMessageInterceptor` for recording |

## Version Compatibility

ULinkActor 0.3.0 is a breaking change from 0.2.x:
- `ActorCallTimeoutReason.CircularWait` removed
- `ActorCell` constructor gains `executionTimeout` parameter
- `ActorSystem.Stop` flow reordered (removal after drain)

ULinkGame must update its `ULinkActor` NuGet reference and adapt the facade accordingly.
