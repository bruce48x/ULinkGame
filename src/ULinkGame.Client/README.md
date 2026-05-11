# ULinkGame.Client

`ULinkGame.Client` contains engine-neutral client helpers for game clients built on top of ULinkRPC.

The package currently focuses on reliable server push and reconnect-aware client state:

- track the latest applied reliable push sequence
- detect duplicate reliable push messages
- decide whether an incoming push should be applied and acknowledged
- reset sequence state when the client starts a new logical session
- expose an engine-neutral session phase snapshot for reconnect, refresh, and state-lost flows

The library does not depend on Unity, Godot, or any transport package. Game clients remain responsible for choosing their transport, dispatching callbacks onto the engine main thread, and applying business-specific payloads.

## Low-level sequence tracking

```csharp
using ULinkGame.Client.ReliablePush;

var tracker = new ReliablePushTracker();
var decision = tracker.Decide(sequence);

if (decision.ShouldApply)
{
    // Apply the business payload.
    tracker.MarkApplied(sequence);
}

if (decision.ShouldAck)
{
    // Send an acknowledgement through the game RPC API.
}
```

## Reliable push inbox

Use `ReliablePushInbox` when the server includes session id and generation in the logical client session. This keeps cursors isolated across reconnect/new-session boundaries.

```csharp
using ULinkGame.Client.ReliablePush;

var session = new ReliablePushSession(ownerKey: playerId, sessionId: sessionId, generation: generation);
var inbox = new ReliablePushInbox();
inbox.StartSession(session, lastAppliedSequence);

await inbox.ProcessAsync(
    ReliablePushSequence.From(update.ReliableSequence),
    update,
    applyAsync: static (payload, ct) =>
    {
        // Apply the business payload on the application's chosen thread.
        return ValueTask.CompletedTask;
    },
    acknowledgeAsync: async (ack, ct) =>
    {
        // Send ack.Session and ack.Sequence.Value through the game's RPC API.
        await playerService.AckReliablePushAsync(ack.Session.SessionId, ack.Session.Generation, ack.Sequence.Value, ct);
        return ReliablePushAckOutcome.Accepted();
    },
    cancellationToken);
```

## Engine-neutral session state

`ClientSessionController` is a pure state helper. Unity, Godot, and plain .NET clients can render their own UI from the snapshot without the framework touching engine APIs or dispatchers.

```csharp
using ULinkGame.Client.ReliablePush;
using ULinkGame.Client.Sessions;

var controller = new ClientSessionController();
controller.StartSession(new ReliablePushSession(playerId, sessionId, generation));
controller.MarkReconnecting();

controller.ApplyAckOutcome(ReliablePushAckOutcome.StateRefreshRequired());

if (controller.Snapshot.Phase == ClientSessionPhase.RefreshRequired)
{
    // Clear transient view state and fetch an authoritative game snapshot.
}

controller.ApplyAckOutcome(ReliablePushAckOutcome.StateLost());

if (controller.Snapshot.Phase == ClientSessionPhase.StateLost)
{
    // Start a new login/session flow. StateLost remains terminal until StartSession is called again.
}
```
