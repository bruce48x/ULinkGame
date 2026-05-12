using ULinkGame.Abstractions;

namespace ULinkGame.Server.ReliablePush;

public delegate ValueTask ReliablePushDeliver<TCallback, in TPayload>(
    TCallback callback,
    ReliablePushSequence sequence,
    TPayload payload,
    CancellationToken cancellationToken)
    where TCallback : class;
