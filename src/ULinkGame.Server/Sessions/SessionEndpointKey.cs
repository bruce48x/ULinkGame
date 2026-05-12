using ULinkGame.Abstractions;

namespace ULinkGame.Server.Sessions;

public readonly record struct SessionEndpointKey(
    GameSessionKey Session,
    string EndpointName);
