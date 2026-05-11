namespace ULinkGame.Server.Sessions;

public readonly record struct GameSessionKey(
    string OwnerKey,
    string SessionId,
    long Generation);

