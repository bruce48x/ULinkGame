using ULinkGame.Cluster;

namespace ULinkGame.Server.Actors;

public sealed class LocalActorNodeIdentity
{
    public LocalActorNodeIdentity(NodeId nodeId)
    {
        NodeId = nodeId;
    }

    public NodeId NodeId { get; }
}
