using ULinkGame.Abstractions;

namespace ULinkGame.Server.Sessions;

public interface IGameSessionResumeService
{
    ValueTask<SessionResumeDecision> TryResumeAsync(
        GameSessionResumeRequest request,
        CancellationToken cancellationToken = default);
}
