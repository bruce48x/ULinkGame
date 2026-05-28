using ULinkGame.Server.Hotfix.Dispatch;

namespace ULinkGame.Server.Hotfix.Scanning;

public sealed record HotfixSystemScanResult(
    IReadOnlyList<HotfixMethodBinding> Methods,
    IReadOnlyList<string> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
