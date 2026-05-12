namespace ULinkGame.Abstractions
{
    public enum SessionResumeStatus
    {
        Resumed,
        StateRefreshRequired,
        StateLost,
        Unauthorized
    }
}
