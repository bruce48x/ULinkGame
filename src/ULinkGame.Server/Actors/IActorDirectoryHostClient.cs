using ULinkGame.Cluster;

namespace ULinkGame.Server.Actors;

public interface IActorDirectoryHostClient
{
    ValueTask<ActorDirectoryRecord?> ResolveAsync(
        NodeId directoryNode,
        ActorId actorId,
        CancellationToken cancellationToken = default);

    ValueTask<ActorDirectoryRegisterStatus> RegisterAsync(
        NodeId directoryNode,
        ActorId actorId,
        NodeId ownerNode,
        CancellationToken cancellationToken = default);

    ValueTask<ActorDirectoryUnregisterStatus> UnregisterAsync(
        NodeId directoryNode,
        ActorId actorId,
        NodeId ownerNode,
        CancellationToken cancellationToken = default);
}
