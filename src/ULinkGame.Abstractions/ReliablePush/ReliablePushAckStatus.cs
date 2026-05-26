namespace ULinkGame.Abstractions
{
    public enum ReliablePushAckStatus
    {
        Accepted,
        Duplicate,
        StateRefreshRequired,
        StateLost,
        SessionMismatch
    }
}
