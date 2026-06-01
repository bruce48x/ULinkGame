namespace ULinkGame.Server.Actors;

public enum RemoteActorStatus
{
    Replied,
    Accepted,
    RouteNotFound,
    Expired,
    Timeout,
    Backpressure,
    HandlerUnavailable,
    NodeUnavailable,
    SerializationFailed,
    DeserializationFailed,
    Cancelled
}
