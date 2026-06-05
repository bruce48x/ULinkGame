namespace ULinkGame.Server.Guardrails;

public sealed record ULinkGameResolvedEndpoint(
    ULinkGameResolvedValue<string> Transport,
    ULinkGameResolvedValue<string> Host,
    ULinkGameResolvedValue<int> Port,
    ULinkGameResolvedValue<string> Path,
    ULinkGameResolvedValue<string> AdvertisedHost,
    ULinkGameResolvedValue<string> AdvertisedEndpoint);
