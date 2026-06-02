using ULinkGame.Cluster;

namespace ULinkGame.Server.Actors;

public interface IActorDirectoryCache
{
    bool TryGet(ActorId actorId, out NodeId node);

    void Set(ActorId actorId, NodeId node);

    void Remove(ActorId actorId);
}
