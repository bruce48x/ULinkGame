namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameResolvedRuntime(
    ULinkGameResolvedValue<string> NodeId,
    IReadOnlyList<ULinkGameResolvedEndpoint> Endpoints,
    ULinkGameResolvedCluster Cluster,
    ULinkGameResolvedClusterEndpoint? ClusterEndpoint,
    ULinkGameResolvedFeature Feature,
    ULinkGameResolvedHotfix Hotfix,
    ULinkGameResolvedReliablePush ReliablePush,
    ULinkGameRuntimeProfile Profile);
