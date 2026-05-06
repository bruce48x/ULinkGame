# ULinkGame.Client

`ULinkGame.Client` contains engine-neutral client helpers for game clients built on top of ULinkRPC.

The package currently focuses on reliable server push consumption:

- track the latest applied reliable push sequence
- detect duplicate reliable push messages
- decide whether an incoming push should be applied and acknowledged
- reset sequence state when the client starts a new logical session

The library does not depend on Unity, Godot, or any transport package. Game clients remain responsible for choosing their transport, dispatching callbacks onto the engine main thread, and applying business-specific payloads.

## Usage

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

