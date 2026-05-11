namespace ULinkGame.Server.Sessions;

public interface IAuthoritativeSessionStateProbe
{
    ValueTask<AuthoritativeSessionStateProbeResult> ProbeAsync(
        GameSessionKey session,
        CancellationToken cancellationToken = default);
}

