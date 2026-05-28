using ULinkGame.Server.Hotfix.Abstractions;

namespace ULinkGame.Server.Hotfix;

public interface IHotfixManager
{
    HotfixSnapshot Current { get; }

    ValueTask<HotfixReloadResult> ReloadAsync(CancellationToken cancellationToken = default);
}
