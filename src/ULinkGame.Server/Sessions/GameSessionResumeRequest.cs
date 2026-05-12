using ULinkGame.Abstractions;

namespace ULinkGame.Server.Sessions;

public sealed record GameSessionResumeRequest(
    GameSessionKey Session,
    string? Token = null);
