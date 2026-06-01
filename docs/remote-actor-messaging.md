# Typed Remote Actor Messaging

ULinkGame remote actor messaging should feel close to skynet's local and cluster call model: local and remote calls use the same business method shape, and only the target selector changes.

The recommended API shape is generated from server-side actor classes:

```csharp
public readonly record struct RoomId(string Value);

public sealed class RoomActor : Actor<RoomId>
{
    public ValueTask<JoinRoomReply> JoinAsync(
        JoinRoomRequest request,
        CancellationToken cancellationToken = default)
    {
        // actor mailbox code
    }

    public ValueTask LeaveAsync(
        LeaveRoomRequest request,
        CancellationToken cancellationToken = default)
    {
        // actor mailbox code
    }
}
```

The source generator emits typed accessors:

```csharp
public sealed class RoomActors
{
    public RoomLocalRef Local(RoomId roomId);

    public RoomRemoteRef Remote(NodeId nodeId, RoomId roomId);
}
```

Business code calls local and remote actors the same way after selecting the target:

```csharp
var localReply = await _rooms
    .Local(roomId)
    .JoinAsync(request, cancellationToken);

var remoteReply = await _rooms
    .Remote(nodeId, roomId)
    .JoinAsync(request, cancellationToken);
```

This keeps day-to-day code short without making remote calls look identical to local calls. `Remote(nodeId, roomId)` is the network boundary.

## Design Goals

- Ordinary game server developers should not hand-write actor ids, route keys, message kinds, serializers, dispatch switches, or reply-correlation plumbing.
- Local and remote actor calls should differ only in target selection, not in every business method call.
- Remote messaging must remain explicit. ULinkGame should not generate transparent distributed actor proxies that hide network failure modes behind local-looking actor references.
- Repeated wrapper code should be generated at compile time. Source generation adds no runtime reflection or dynamic dispatch requirement.
- Server-internal remote actor contracts belong in server assemblies, not in the client-facing `Shared` project.

## Generated API

For each eligible `Actor<TKey>` subclass in a server-side assembly, the generator emits one actor accessor group:

```csharp
public sealed class RoomActors
{
    public RoomLocalRef Local(RoomId id);

    public RoomRemoteRef Remote(NodeId node, RoomId id);
}

public readonly struct RoomLocalRef
{
    public ValueTask<JoinRoomReply> JoinAsync(
        JoinRoomRequest request,
        CancellationToken cancellationToken = default);

    public ValueTask LeaveAsync(
        LeaveRoomRequest request,
        CancellationToken cancellationToken = default);
}

public readonly struct RoomRemoteRef
{
    public ValueTask<JoinRoomReply> JoinAsync(
        JoinRoomRequest request,
        CancellationToken cancellationToken = default);

    public ValueTask LeaveAsync(
        LeaveRoomRequest request,
        CancellationToken cancellationToken = default);
}
```

The generated `Local(...)` ref invokes the process-local `IActorRuntime`.

The generated `Remote(node, ...)` ref serializes the request, sends a cluster actor envelope through the remote actor invoker, waits for a reply when the actor method returns a value, deserializes the reply, and maps delivery failures to `RemoteActorException`.

The business method surface is intentionally not doubled with `TryJoinAsync` or `TryLeaveAsync`. Normal remote actor calls return normally or throw. Lower-level result-returning APIs remain available for framework internals and rare boundary services.

## Actor Key Model

Actor key type is declared in the actor base type:

```csharp
public sealed class RoomActor : Actor<RoomId>
{
}
```

This avoids separate `[ActorKey]` attributes and avoids generator guessing. The generator uses `TKey` to type `Local(TKey id)` and `Remote(NodeId node, TKey id)`.

Default key-to-string conversion:

1. If `TKey` has a readable `Value` property, use `Value.ToString()`.
2. Otherwise use `TKey.ToString()`.

Default actor id shape:

```txt
<actor-name>/<key-value>
```

Examples:

```txt
RoomActor + RoomId("1001") -> room/1001
PlayerActor + PlayerId("alice") -> player/alice
```

Default actor name is derived from the class name by trimming the `Actor` suffix and converting to lower camel/kebab-free form:

```txt
RoomActor -> room
PlayerActor -> player
MatchmakingActor -> matchmaking
```

Long-lived protocols can pin the wire name:

```csharp
[ActorName("room")]
public sealed class BattleRoomActor : Actor<RoomId>
{
}
```

## Generated Method Rules

The generator exposes public instance methods with one of these shapes:

```csharp
public ValueTask MethodAsync(
    TRequest request,
    CancellationToken cancellationToken = default);

public ValueTask<TReply> MethodAsync(
    TRequest request,
    CancellationToken cancellationToken = default);
```

Methods with unsupported signatures are not exposed as remote methods. The generator should report diagnostics for ambiguous or likely accidental public methods.

Default method id is derived from the method name:

```txt
JoinAsync -> join
LeaveAsync -> leave
```

Long-lived protocols can pin the wire method id:

```csharp
[ActorMethod("join")]
public ValueTask<JoinRoomReply> JoinAsync(
    JoinRoomRequest request,
    CancellationToken cancellationToken = default);
```

Useful attributes:

```csharp
[ActorName("room")]       // stable actor wire name
[ActorMethod("join")]     // stable method wire id
[ActorIgnore]             // do not expose a public method
[ActorLocalOnly]          // do not generate remote refs for this actor
```

Attributes adjust defaults; they are not required for the common path.

## Failure Model

Generated business methods use the skynet-like model: return a reply on success, throw on remote failure.

```csharp
try
{
    var reply = await _rooms
        .Remote(nodeId, roomId)
        .JoinAsync(request, cancellationToken);
}
catch (RemoteActorException ex) when (ex.Status == RemoteActorStatus.RouteNotFound)
{
    // room has gone away or was never registered on that node
}
```

`RemoteActorException` carries structured failure details:

```csharp
public sealed class RemoteActorException : Exception
{
    public RemoteActorStatus Status { get; }

    public NodeId? Node { get; }

    public ActorId ActorId { get; }

    public string ActorName { get; }

    public string MethodName { get; }

    public string? CorrelationId { get; }
}
```

Initial status values should cover normal distributed failure modes:

```csharp
public enum RemoteActorStatus
{
    RouteNotFound,
    Expired,
    Timeout,
    Backpressure,
    HandlerUnavailable,
    NodeUnavailable,
    SerializationFailed,
    DeserializationFailed,
    Cancelled
}
```

The generated API should not require ordinary call sites to switch over these statuses. Boundary services that need status-returning behavior can use the lower-level invoker.

## Runtime Layers

The generated typed API sits above existing cluster primitives:

```txt
game service code
  -> generated RoomActors.Local/Remote refs
  -> local actor invoker / remote actor invoker
  -> IActorRuntime / IClusterRouter
  -> ClusterActorEnvelope
  -> ClusterMessage / RouteLocation / transport adapter
```

The lower-level `ClusterMessage`, `ClusterActorEnvelope`, `IClusterRouter`, and remote actor invoker remain important. They are implementation foundations and escape hatches, not the recommended daily business API.

## Local Calls

`Local(id)` uses the process-local actor runtime. Generated local refs should avoid serialization and cluster envelope allocation. They should call `IActorRuntime.AskAsync` or `TellAsync` with generated delegates.

Local calls may still fail for local actor runtime reasons such as unavailable actors, stopped actors, mailbox capacity, or execution timeout. Those failures should map to local actor exceptions or existing actor runtime result types, not to `RemoteActorException`.

## Remote Calls

`Remote(node, id)` uses the cluster layer. Generated remote refs should:

1. Build a stable actor id from actor name and key.
2. Build a stable method id.
3. Serialize the request through the configured serializer.
4. Create a cluster actor envelope with an absolute deadline.
5. Send through the remote actor invoker.
6. For `ValueTask<TReply>` methods, wait for the correlated reply.
7. Deserialize the reply.
8. Throw `RemoteActorException` for remote delivery or reply failures.

The reply route should be registered at node startup, not per request. Per-request work should be limited to correlation registration, payload encoding, and message send.

## Route-Based Calls

Do not make automatic route lookup the first typed API. Start with explicit:

```csharp
_rooms.Local(roomId)
_rooms.Remote(nodeId, roomId)
```

Later, after route-directory behavior, cache invalidation, node epoch handling, stale route retries, and migration semantics are proven, the generator can add:

```csharp
_rooms.Route(roomId).JoinAsync(request, cancellationToken);
```

This preserves the initial API while adding a higher-level target selector when the routing policy is mature.

## Server-Side Boundary

Typed remote actor generation is server-side infrastructure. It should scan server assemblies and generate server-only code.

Do not place remote actor declarations in the client-facing `Shared` project. `Shared` remains for client/server DTOs and RPC contracts. If a request or reply DTO is also needed by the client, that DTO can live in `Shared`; the actor class, generated actor refs, remote actor attributes, route keys, and invoker types stay server-side.

## Compatibility Guidance

The easy default path should need no attributes:

```csharp
public readonly record struct RoomId(string Value);

public sealed class RoomActor : Actor<RoomId>
{
    public ValueTask<JoinRoomReply> JoinAsync(
        JoinRoomRequest request,
        CancellationToken cancellationToken = default);
}
```

For actors and methods that become durable wire protocols, pin names explicitly:

```csharp
[ActorName("room")]
public sealed class RoomActor : Actor<RoomId>
{
    [ActorMethod("join")]
    public ValueTask<JoinRoomReply> JoinAsync(
        JoinRoomRequest request,
        CancellationToken cancellationToken = default);
}
```

This lets class and method names evolve without changing actor ids or message ids.

## Relation To Current Low-Level API

The current `AskRemoteAsync` and `TellRemoteAsync` extension methods prove the plumbing for cluster actor envelopes, reply correlation, and dispatcher composition. They are too low-level for frequent business use because callers must provide actor id strings, method kind strings, serialization delegates, reply deserialization delegates, and timeouts at every call site.

The typed API should replace those extensions as the recommended documentation path. The lower-level API can remain as an escape hatch or be moved behind the generated remote actor invoker.
