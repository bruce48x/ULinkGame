using ULinkGame.Abstractions;

namespace ULinkGame.Server.Sessions;

public interface IGameSessionEndpointCloser
{
    ValueTask CloseEndpointAsync(
        SessionEndpointKey endpoint,
        string connectionId,
        SessionTerminationNotice notice,
        CancellationToken cancellationToken = default);
}
