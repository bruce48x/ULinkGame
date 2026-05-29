# ULinkGame Design Philosophy

## What ULinkGame Is

ULinkGame is a **distributed game server framework** built on two lower-level libraries:

- **ULinkActor** — process-local actor runtime (mailbox, lifecycle, timers, diagnostics)
- **ULinkRPC** — transport, serialization, and RPC code generation

ULinkGame adds what games need on top: sessions, reliable message delivery, cluster routing, hot-reloadable business logic, and opinionated patterns for building multiplayer game servers.

## Influences

ULinkGame's design is informed by four reference frameworks:

| Framework | Language | Key strength |
|-----------|----------|-------------|
| [skynet](https://github.com/cloudwu/skynet) | C/Lua | Pragmatic simplicity, fault isolation, decade of production use |
| [ET](https://github.com/egametang/ET) | C# | Component-based assembly, fiber model, AI-native architecture |
| [Fantasy](https://github.com/qq362946/Fantasy) | C# | Zero-reflection source generation, roaming route system |
| [GeekServer](https://github.com/leeveel/GeekServer) | C# | Compile-time enforcement, TPL Dataflow actor model |

**skynet is the primary influence.** Its philosophy of "simple core, explicit boundaries, fail fast" directly shapes ULinkGame's architecture. The other three C# frameworks provide inspiration for developer experience and tooling, but their design choices are evaluated against skynet's principles before adoption.

## Core Principles

### 1. skynet compatibility — the litmus test

Every design decision is evaluated against this question: **"Would skynet's author agree with this?"**

If a feature from ET, Fantasy, or GeekServer conflicts with skynet's philosophy, skynet wins. Specifically:

- **Visible remote boundaries over transparent distribution.** Cross-node communication uses different APIs than local actor calls. The developer always knows when they are crossing a network boundary.
- **Fail fast over silent recovery.** Design errors (circular calls, lost state) throw immediately rather than retrying or degrading.
- **Bounded resources over unbounded queues.** Every queue, cache, and timeout has an explicit limit.
- **Independent sandboxes over shared fate.** One actor's failure must not cascade.

### 2. Explicit boundaries between layers

```
Application (game logic, matchmaking, persistence)
    └─ ULinkGame (sessions, reliable push, cluster, hotfix)
        └─ ULinkRPC (transport, serialization, RPC)
        └─ ULinkActor (mailbox, lifecycle, timers)
            └─ .NET (thread pool, TPL Dataflow, System.Threading)
```

Each layer has a well-defined responsibility. Lower layers do not know about higher layers. ULinkActor does not know about networking. ULinkRPC does not know about game sessions. ULinkGame does not contain game logic.

### 3. Node is the deployment unit

A node is one OS process. Services (gateway, lobby, room) are composed inside a node through configuration. In development, all services run in one process. In production, they are split across multiple processes — but the code is identical. Only the configuration changes.

This is the "N → 1, 1 → N" pattern observed in ET: develop with everything in one process for easy debugging, deploy with services split for scale.

### 4. At-least-once with idempotent receivers

The network is unreliable. Rather than attempting perfect exactly-once delivery (impossible in the general case), ULinkGame provides **at-least-once reliable push** with monotonically increasing sequence numbers. Receivers detect duplicates and apply each message exactly once.

When server state is lost (crash, restart), the client receives an explicit "state lost" signal rather than silently corrupting data. This is a first-class design choice, not an error condition.

### 5. Framework scope is intentionally narrow

ULinkGame does **not** provide:

- Account systems or authentication
- Matchmaking algorithms
- Game-specific data models
- Persistence schemas
- Client-side rendering or physics

These belong to game projects. The framework provides infrastructure; the game provides content.

## Framework Analysis: What We Absorb and What We Reject

### Absorbed (implemented or planned)

| Feature | Source | Status | Rationale |
|---------|--------|--------|-----------|
| Actor mailbox + diagnostics | skynet | Done (ULinkActor) | Core concurrency model |
| Reliable push (at-least-once) | skynet (message log concept) | Done (ULinkGame) | Business-level delivery guarantee |
| Hot-reloadable business logic | skynet (Lua hotswap) | Done (ULinkGame.Hotfix) | Zero-downtime logic updates |
| Explicit cluster routing | skynet (harbor) | Done (ULinkGame.Cluster) | Cross-node messaging with visible boundaries |
| Session lifecycle + reconnect | skynet (gate/watchdog/agent) | Done (ULinkGame.Server) | Connection management |
| Component-based assembly (N→1, 1→N) | ET | Planned | Single-process dev, multi-process prod |
| Cross-server event bus | Fantasy (SphereEvent) | Planned | Pub-sub for announcements, leaderboards |
| Location-aware actor messaging | ET | Planned | `AskRemoteAsync` with explicit Remote naming |
| Gate auto-routing (Roaming) | Fantasy | Planned | Client-transparent backend routing |
| Service discovery + leader election | ET | Planned | Automatic failover |
| Deadlock detection → immediate failure | GeekServer (adapted) | Done (ULinkActor 0.3.0) | Circular calls throw synchronously |
| Execution timeout | skynet (monitor + signal) | Done (ULinkActor 0.3.0) | Stuck actor recovery |
| Message recording hooks | skynet (message log replay) | Done (ULinkActor 0.3.0) | Interceptor for recording/replay |
| Actor state machine | skynet (service lifecycle) | Done (ULinkActor 0.3.0) | Explicit Active→Draining→Dead |

### Rejected (conflicts with skynet philosophy)

| Feature | Source | Why rejected |
|---------|--------|-------------|
| Fully transparent distributed actors | ET | Hides network latency and failure modes behind local-looking APIs |
| Actor = Entity (ECS merged with Actor) | ET, Fantasy | Conflates concurrency unit with data container, leads to overly fine-grained remote calls |
| One-click network calls (network disguised as local method) | Fantasy | Makes remote cost invisible; violates "remote boundaries are visible" |
| Kestrel as network layer | GeekServer | ULinkRPC already provides transport abstraction |
| TPL Dataflow as sole actor backend | GeekServer | ULinkActor already provides this |
| Transparent persistence | GeekServer | Persistence is a game-layer concern, not a framework concern |

### Not applicable (different language or domain)

| Feature | Source | Why not applicable |
|---------|--------|--------------------|
| Lua VM per service | skynet | C# uses AssemblyLoadContext for isolation |
| Coroutine pool | skynet | .NET has ValueTask pooling built in |
| Cross-VM proto sharing | skynet | C# type system provides equivalent sharing |
| Behavior tree / Buff system | ET | Game content, not framework infrastructure |
| Excel config export toolchain | GeekServer | Game tooling, not framework concern |
| AI Skill for framework | Fantasy, ET | Can be added later as CLAUDE.md enhancements |

## Design Decisions Log

### Why string-based ActorId in ULinkGame when ULinkActor uses long?

ULinkActor uses `long` for process-local actor identity (fast, monotonic, non-reusable). ULinkGame uses `string` for game-level identity because game entities need human-readable, cross-process identifiers (e.g., `player:alice`, `room:42`). The string is hashed to a ULinkActor `long` when interacting with the local runtime.

This mirrors skynet's 32-bit address scheme (8-bit node + 24-bit local) but with more flexibility for game-specific naming.

### Why explicit cluster API instead of transparent routing?

skynet's harbor system requires explicit cross-node addressing. ULinkGame follows the same model: `IClusterRouter.SendAsync()` is a different API from `IActorRuntime.TellAsync()`. This makes the cost and failure modes of cross-node communication visible at the call site.

The planned `AskRemoteAsync` extension method will simplify the syntax but keep the "Remote" naming to preserve visibility.

### Why at-least-once instead of exactly-once?

Exactly-once delivery in a distributed system requires distributed consensus (e.g., two-phase commit), which is too expensive for real-time game messages. At-least-once with idempotent receivers and monotonic sequence numbers provides the same correctness guarantee at a fraction of the cost.

This is the approach used by TCP (sequence numbers + retransmission) and Kafka (offset tracking), adapted for game sessions.

### Why hotfix DLLs instead of Lua or JavaScript?

.NET's `AssemblyLoadContext` provides collectible assembly loading with full access to the C# type system. Hotfix assemblies can reference stable game types directly, with source-generated friend accessors for private state. This preserves type safety and debugging while enabling zero-downtime logic updates.

The tradeoff is that hotfix assemblies cannot modify state layout — only behavior operating on existing state. This is intentional: stable state + replaceable logic is a cleaner separation than "everything is hot-swappable."

## Roadmap

### Phase 1: Foundation hardening (current)

- [x] ULinkActor 0.3.0: execution timeout, state machine, interceptor hooks, circular call fast-fail
- [ ] ULinkGame adapts ULinkActor 0.3.0 features (expose config, wire interceptor for message recording)
- [ ] Gate/Watchdog/Agent pattern documented and scaffolded by `ULinkGame.Tool`

### Phase 2: Developer experience

- [ ] Component-based assembly (`IGameComponent`, single-process dev / multi-process prod)
- [ ] Location-aware actor messaging (`AskRemoteAsync` / `TellRemoteAsync`)
- [ ] Full-link test framework (`ULinkGame.Testing`)

### Phase 3: Distributed operations

- [ ] Cross-server event bus (`IClusterEventBus`)
- [ ] Gate auto-routing (client-transparent backend routing)
- [ ] Service discovery with leader election
- [ ] Soft routing anti-DDoS

### Phase 4: Production hardening

- [ ] Message recording/replay on ULinkGame layer (using ULinkActor interceptor)
- [ ] Distributed tracing across cluster boundaries (using ULinkActor Activity propagation)
- [ ] Systematic resource boundaries (session count, cluster send queue, route table size)
