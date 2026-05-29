using ULinkGame.Server.Actors;

namespace ULinkGame.Server.Diagnostics;

public sealed record MessageLogEntry(
    DateTimeOffset Timestamp,
    object Message,
    string? Error);
