using ULinkGame.Cluster;

namespace ULinkGame.Server.Actors;

public sealed record RemoteActorInvocation(
    NodeId Node,
    ActorId ActorId,
    string ActorName,
    string MethodName,
    ReadOnlyMemory<byte> Payload,
    DateTimeOffset Deadline,
    string CorrelationId);
