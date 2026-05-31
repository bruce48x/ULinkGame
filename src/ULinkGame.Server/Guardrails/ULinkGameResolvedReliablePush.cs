namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameResolvedReliablePush(
    ULinkGameResolvedValue<string> StorageMode,
    ULinkGameResolvedValue<int> PendingLimit,
    ULinkGameResolvedValue<int> ReplayWindowSeconds,
    bool HasSessionIdentityResolver);
