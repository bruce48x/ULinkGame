# Gate / Watchdog / Agent — Connection Management Pattern

This is a recommended architecture pattern, not a framework class. It describes how to compose ULinkGame's existing infrastructure into a proven connection management model. The pattern originates from [skynet](https://github.com/cloudwu/skynet).

## The pattern

```
Client ──TCP──→ Gate ──→ Watchdog ──→ Agent (one per player)
```

| Role | Responsibility | Has business state | Failure impact |
|------|---------------|:---:|------|
| **Gate** | Maintain TCP connections, forward messages. No business logic. | No | Disconnect → reconnect to another Gate, agent unchanged |
| **Watchdog** | Authenticate, create/bind Agent, then exit the call chain. | Transient | Only affects new connections |
| **Agent** | One-to-one player service. Holds all session state. | Yes | Only affects that player |

The key insight: **Gate is stateless.** If a Gate process goes down, the client reconnects to another Gate, Watchdog finds the existing Agent, and the session continues. Cheap Gate nodes can be exposed to the public internet; expensive Agent nodes stay behind.

## Dual-channel variant

For low-latency games (fighting games, FPS), add a realtime channel that bypasses Gate:

```
                    ┌─── Gate ─── Watchdog ─── Agent (control, low-freq)
Client ──┬──────────┤
         │
         └─── KCP direct ─── Room (realtime, 30fps)
```

The control channel handles login, matchmaking, reconnect. The realtime channel handles frame input and state snapshots. They are independent — losing one doesn't impact the other.

## How to implement with ULinkGame

ULinkGame provides all the mechanisms. The pattern is just composition:

| What you need | ULinkGame mechanism |
|---------------|-------------------|
| Gate: TCP/WS listener | `IULinkRpcServerConfigurator` with TCP or WebSocket transport |
| Gate → Agent routing | `IClusterRouter` + `IRouteDirectory` |
| Watchdog: auth + agent creation | `IGameSessionTokenValidator` + `IULinkGameServer.StartSessionAsync` |
| Agent: per-player service | `IActorRuntime` with per-player `ActorId` |
| Reconnect to another Gate | `GameSessionResumeService` with resume token |
| Realtime channel | `IULinkRpcServerConfigurator` with KCP transport, separate endpoint |
| Reliable delivery | `IReliablePushOutbox` + `IReliablePushInbox` |

## When to use which variant

| Game type | Recommendation |
|-----------|---------------|
| Turn-based, casual, light MMO | Classic Gate → Watchdog → Agent. Single TCP/WS connection. |
| Real-time PvP, fighting, FPS | Dual-channel. Control via Gate, realtime via KCP direct to Room. |
| Single-server, single-player | Don't use this pattern. One process, no Gate needed. |
