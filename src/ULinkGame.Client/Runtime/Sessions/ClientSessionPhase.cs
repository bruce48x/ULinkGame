namespace ULinkGame.Client.Sessions
{
    public enum ClientSessionPhase
    {
        SignedOut,
        Connecting,
        Active,
        Reconnecting,
        RefreshRequired,
        StateLost
    }
}

