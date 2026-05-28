namespace ULinkGame.Server.Hotfix.Abstractions;

public sealed record HotfixSnapshot(
    string? Version,
    string? SourceKind,
    string? SourcePath,
    DateTimeOffset? LoadedAtUtc,
    long DispatchTableVersion,
    IReadOnlyList<HotfixMethodKey> Methods,
    HotfixReloadStatus? LastReloadStatus,
    string? LastFailureMessage,
    string? LastFailureExceptionType);
