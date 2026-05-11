namespace ULinkGame.Client.ReliablePush
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

