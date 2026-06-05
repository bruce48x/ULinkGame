namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameResolvedClusterEndpoint(
    ULinkGameResolvedValue<string> Endpoint,
    IReadOnlyList<string> Seeds);
