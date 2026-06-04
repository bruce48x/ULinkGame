# Session Termination Notice Design

## Context

Game servers sometimes need to remove a player from an active session and close the underlying connection. Before closing the transport, the server should attempt to tell the client why the session is ending. Examples include duplicate login, account ban, server maintenance, GM action, anti-cheat action, session generation mismatch, or policy-driven shutdown.

This is a game-session lifecycle concern. ULinkRPC remains responsible for transport, serialization, RPC invocation, generated bindings, and generic connection close behavior. ULinkRPC must not know about players, sessions, kick reasons, reconnect policy, or game-owned termination semantics.

## Recommended Placement

The protocol belongs in ULinkGame:

- `ULinkGame.Abstractions` owns the fixed minimal cross-side notice contract.
- `ULinkGame.Server` owns server-side termination orchestration: mark the session terminal, stop accepting new work for that session, send the fixed notice, wait only a bounded time, and then close or request closure of the underlying endpoint.
- `ULinkGame.Client` owns engine-neutral terminal state helpers for applying the fixed notice exactly once.
- User-owned game code chooses when to terminate and what reason/message to pass, but does not define a separate wire protocol.
- ULinkRPC may expose generic transport primitives such as flush-before-close or close status, but not a kick-player protocol.

## Protocol Shape

Keep the framework protocol fixed and small. ULinkGame should define the session termination wire shape, but should not define a rich product-specific reason catalog or UI text system.

Suggested shared contract:

```csharp
public sealed class SessionTerminationNotice
{
    public required GameSessionKey Session { get; init; }
    public required SessionTerminationReason Reason { get; init; }
    public string? Message { get; init; }
    public DateTimeOffset IssuedAt { get; init; }
}

public enum SessionTerminationReason
{
    ReplacedByNewLogin,
    ServerShutdown,
    Maintenance,
    Unauthorized,
    Policy,
    StateLost,
    Application
}
```

`Reason` is the framework-level reason used by client state logic. `Message` is optional display-oriented context. Do not add a second reason-code field in the first version; it makes the API ambiguous and invites product-specific catalogs into the framework.

```csharp
public sealed class SessionTerminationOptions
{
    public TimeSpan NotifyTimeout { get; init; } = TimeSpan.FromSeconds(1);
    public bool KeepTerminalStateForResume { get; init; } = true;
}
```

The callback method should also be fixed:

```csharp
public interface IULinkGameSessionCallback
{
    ValueTask OnSessionTerminatedAsync(
        SessionTerminationNotice notice,
        CancellationToken cancellationToken = default);
}
```

ULinkGame's value is the fixed terminal contract plus the ordered workflow: commit terminal session state, attempt notification, then close.

## User Experience

The common API should default to the control endpoint:

```csharp
public ValueTask TerminateSessionAsync(
    GameSessionKey session,
    SessionTerminationReason reason,
    string? message = null,
    SessionTerminationOptions? options = null,
    CancellationToken cancellationToken = default);
```

Games with multiple client-facing endpoints can use an explicit endpoint overload:

```csharp
public ValueTask TerminateSessionAsync(
    GameSessionKey session,
    GameEndpointName endpointName,
    SessionTerminationReason reason,
    string? message = null,
    SessionTerminationOptions? options = null,
    CancellationToken cancellationToken = default);
```

The intended usage should look like this:

```csharp
await gameServer.TerminateSessionAsync(
    session,
    SessionTerminationReason.ReplacedByNewLogin,
    message: "This account logged in elsewhere.");
```

ULinkGame resolves the current `IULinkGameSessionCallback` on `GameEndpointName.Control`, sends `SessionTerminationNotice`, waits up to `NotifyTimeout`, and closes the endpoint. Callers pass `cancellationToken` only when they need to bind the operation to a wider server workflow.

The minimal user steps are:

1. Make the client callback endpoint implement `IULinkGameSessionCallback`.
2. Register or configure the endpoint closer used by ULinkGame to close a stored `connectionId`.
3. Call `TerminateSessionAsync` when server policy decides the session must end.
4. On the client, handle `OnSessionTerminatedAsync` by showing UI and clearing local session state.

## Server Flow

1. Validate that the target session generation is still current.
2. Mark the session as terminating or terminated before sending the notice so new business calls are rejected deterministically.
3. Build a `SessionTerminationNotice`.
4. Invoke `IULinkGameSessionCallback.OnSessionTerminatedAsync` against the current callback endpoint.
5. Wait only within `SessionTerminationOptions.NotifyTimeout`.
6. Close the underlying endpoint when the notification finishes or when the timeout expires.
7. Preserve enough terminal session state that a reconnect attempt can return the same termination outcome instead of looking like an unrelated network failure.

The close path must be best-effort. The framework cannot guarantee that the client receives the final notice before the network disappears.

## Client Flow

1. Receive `SessionTerminationNotice`.
2. Apply the terminal transition exactly once.
3. Stop normal reconnect behavior unless the game explicitly starts a new login/session flow.
4. Surface the termination reason through game UI. Unity, Godot, and UI-specific behavior remains outside the core package.

If the client only observes a disconnection and never receives the notice, it should follow the normal reconnect or login path. The server-side resume/login response must then expose the terminal state or reject the session explicitly.

## Failure Handling

The protocol must not promise guaranteed delivery before disconnect. It should promise:

- server-side terminal state is committed before the transport is closed;
- the server attempts to send the notice;
- notification is bounded by timeout;
- reconnect or resume can rediscover the terminal outcome;
- duplicate notices are safe for the client to handle idempotently;
- stale session generations are ignored or rejected.

This keeps the design compatible with unreliable networks and avoids pretending that a final packet is always delivered.

## Alternatives Considered

### Put kick semantics in ULinkRPC

This is rejected. It would make ULinkRPC aware of higher-level player and session concepts, weakening the product boundary where ULinkRPC is only transport, serialization, and RPC machinery.

### Treat termination as an ordinary game reliable push

This is usable for application-specific games, but too loose for framework-level session lifecycle. A termination notice should have one fixed shape so ULinkGame.Client can provide a consistent terminal state helper and reconnect can return a predictable outcome.

### Leave everything to game projects

This avoids framework API surface, but every game server needs some version of session termination, duplicate-login handling, and reconnect rejection. A narrow ULinkGame-level terminal control event fits the existing session and reliable push responsibilities without turning ULinkGame into a business framework.

## Implementation Direction

Start small:

1. Add `SessionTerminationNotice`, `SessionTerminationReason`, and `IULinkGameSessionCallback` to the shared framework contract surface.
2. Extend server session state with a terminal outcome that survives endpoint close and can inform resume/login decisions.
3. Add `SessionTerminationOptions` and `TerminateSessionAsync` to `ULinkGame.Server`.
4. Extend the client session controller with a minimal terminal state helper.
5. Add unit tests for timeout, duplicate termination, stale generation, reconnect-after-termination, and direct-disconnect fallback.

Do not add product-specific reason catalogs, UI text, Unity-specific behavior, durable audit storage, or ULinkRPC-level kick semantics in the first version.
