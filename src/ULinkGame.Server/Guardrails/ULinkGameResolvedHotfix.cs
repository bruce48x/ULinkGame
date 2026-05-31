namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameResolvedHotfix(
    ULinkGameResolvedValue<string> AssemblyPath,
    ULinkGameResolvedValue<string> AssemblyFileName);
