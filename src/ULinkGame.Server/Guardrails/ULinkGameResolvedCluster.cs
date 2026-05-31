namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameResolvedCluster(
    IReadOnlyList<ULinkGameResolvedClusterService> Services,
    IReadOnlyDictionary<string, string> AdvertisedEndpoints);

public sealed record ULinkGameResolvedClusterService(
    string Kind,
    string Name);
