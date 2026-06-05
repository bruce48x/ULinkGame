namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameResolvedFeature(
    IReadOnlyList<string>? Configured,
    IReadOnlyList<string> Active,
    IReadOnlyList<string> StartupOrder);
