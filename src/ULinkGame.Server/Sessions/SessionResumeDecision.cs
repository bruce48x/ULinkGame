using ULinkGame.Abstractions;

namespace ULinkGame.Server.Sessions;

public sealed record SessionResumeDecision(
    SessionResumeStatus Status,
    GameSessionKey? Session,
    string? Reason = null)
{
    public static SessionResumeDecision Resumed(GameSessionKey session)
    {
        return new SessionResumeDecision(SessionResumeStatus.Resumed, session);
    }

    public static SessionResumeDecision StateLost(string? reason = null)
    {
        return new SessionResumeDecision(SessionResumeStatus.StateLost, null, reason);
    }

    public static SessionResumeDecision StateRefreshRequired(
        GameSessionKey session,
        string? reason = null)
    {
        return new SessionResumeDecision(SessionResumeStatus.StateRefreshRequired, session, reason);
    }

    public static SessionResumeDecision Unauthorized(string? reason = null)
    {
        return new SessionResumeDecision(SessionResumeStatus.Unauthorized, null, reason);
    }
}
