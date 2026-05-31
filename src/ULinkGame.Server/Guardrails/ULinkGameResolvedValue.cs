namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameResolvedValue<T>(
    T Value,
    ULinkGameValueSource Source,
    string? Path = null);
