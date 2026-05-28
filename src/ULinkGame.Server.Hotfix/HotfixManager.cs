using ULinkGame.Server.Hotfix.Abstractions;
using ULinkGame.Server.Hotfix.Dispatch;
using ULinkGame.Server.Hotfix.Loading;
using ULinkGame.Server.Hotfix.Scanning;

namespace ULinkGame.Server.Hotfix;

public sealed class HotfixManager : IHotfixManager
{
    private readonly IHotfixAssemblySource _source;
    private long _nextVersion;
    private HotfixSnapshot _current = new(null, null, null, null, 0, Array.Empty<HotfixMethodKey>(), null, null, null);
    private HotfixAssemblyLoadContext? _loadContext;

    public HotfixManager(IHotfixAssemblySource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public HotfixSnapshot Current => Volatile.Read(ref _current);

    public async ValueTask<HotfixReloadResult> ReloadAsync(CancellationToken cancellationToken = default)
    {
        HotfixAssemblySourceResult? resolved = null;
        HotfixAssemblyLoadContext? pendingContext = null;
        try
        {
            resolved = await _source.ResolveAsync(cancellationToken).ConfigureAwait(false);
            if (!File.Exists(resolved.AssemblyPath))
            {
                throw new FileNotFoundException("Hotfix assembly was not found.", resolved.AssemblyPath);
            }

            pendingContext = new HotfixAssemblyLoadContext(resolved.AssemblyPath);
            var assembly = pendingContext.LoadFromAssemblyPath(resolved.AssemblyPath);
            var scan = HotfixSystemScanner.Scan(assembly);
            if (!scan.Succeeded)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, scan.Diagnostics));
            }

            var tableVersion = Interlocked.Increment(ref _nextVersion);
            var table = new HotfixDispatchTable(tableVersion, scan.Methods);
            HotfixDispatch.Replace(table);

            var oldContext = Interlocked.Exchange(ref _loadContext, pendingContext);
            pendingContext = null;
            oldContext?.Unload();

            var snapshot = new HotfixSnapshot(
                resolved.Version,
                resolved.SourceKind,
                resolved.AssemblyPath,
                DateTimeOffset.UtcNow,
                tableVersion,
                table.MethodKeys,
                HotfixReloadStatus.Succeeded,
                null,
                null);
            Volatile.Write(ref _current, snapshot);

            return new HotfixReloadResult(HotfixReloadStatus.Succeeded, snapshot, resolved.Version, resolved.AssemblyPath, Array.Empty<string>());
        }
        catch (Exception ex)
        {
            pendingContext?.Unload();

            var previous = Current;
            var snapshot = new HotfixSnapshot(
                previous.Version,
                previous.SourceKind,
                previous.SourcePath,
                previous.LoadedAtUtc,
                previous.DispatchTableVersion,
                previous.Methods,
                HotfixReloadStatus.Failed,
                ex.Message,
                ex.GetType().FullName);
            Volatile.Write(ref _current, snapshot);
            return new HotfixReloadResult(
                HotfixReloadStatus.Failed,
                snapshot,
                resolved?.Version,
                resolved?.AssemblyPath,
                [ex.Message],
                ex.Message,
                ex.GetType().FullName);
        }
    }
}
