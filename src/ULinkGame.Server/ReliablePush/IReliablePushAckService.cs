using ULinkGame.Server.Sessions;

namespace ULinkGame.Server.ReliablePush;

public interface IReliablePushAckService
{
    ValueTask<ReliablePushAckOutcome> AckAsync(
        GameSessionKey currentSession,
        GameSessionKey acknowledgedSession,
        long sequence,
        CancellationToken cancellationToken = default);
}

