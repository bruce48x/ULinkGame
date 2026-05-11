namespace ULinkGame.Server.ReliablePush;

public enum ReliablePushAckStatus
{
    Accepted,
    Duplicate,
    StateRefreshRequired,
    StateLost,
    SessionMismatch
}

