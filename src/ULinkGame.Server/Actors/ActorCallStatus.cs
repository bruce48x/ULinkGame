namespace ULinkGame.Server.Actors;

public enum ActorCallStatus
{
    ActorNotFound,
    NodeUnavailable,
    Timeout,
    Backpressure,
    Expired,
    Failed
}
