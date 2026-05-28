namespace ULinkGame.Server.Hotfix.Abstractions;

public sealed record HotfixReloadResult(
    HotfixReloadStatus Status,
    HotfixSnapshot Current,
    string? RequestedVersion,
    string? RequestedPath,
    IReadOnlyList<string> Diagnostics,
    string? ErrorMessage = null,
    string? ExceptionType = null)
{
    public bool Succeeded => Status == HotfixReloadStatus.Succeeded;
}
