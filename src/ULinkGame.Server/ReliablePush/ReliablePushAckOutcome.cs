using ULinkGame.Server.Sessions;

namespace ULinkGame.Server.ReliablePush;

public sealed record ReliablePushAckOutcome(
    ReliablePushAckStatus Status,
    GameSessionKey? Session = null,
    long Sequence = 0,
    string? Reason = null)
{
    public static ReliablePushAckOutcome Accepted(GameSessionKey session, long sequence)
    {
        return new ReliablePushAckOutcome(ReliablePushAckStatus.Accepted, session, sequence);
    }

    public static ReliablePushAckOutcome Duplicate(GameSessionKey session, long sequence)
    {
        return new ReliablePushAckOutcome(ReliablePushAckStatus.Duplicate, session, sequence);
    }

    public static ReliablePushAckOutcome StateLost(string? reason = null)
    {
        return new ReliablePushAckOutcome(ReliablePushAckStatus.StateLost, Reason: reason);
    }

    public static ReliablePushAckOutcome SessionMismatch(string? reason = null)
    {
        return new ReliablePushAckOutcome(ReliablePushAckStatus.SessionMismatch, Reason: reason);
    }
}

