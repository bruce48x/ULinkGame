namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameResolvedRuntime(
    ULinkGameResolvedValue<string> NodeId,
    ULinkGameResolvedEndpoint Endpoint,
    ULinkGameResolvedCluster Cluster,
    ULinkGameResolvedHotfix Hotfix,
    ULinkGameResolvedReliablePush ReliablePush,
    ULinkGameRuntimeProfile Profile);
