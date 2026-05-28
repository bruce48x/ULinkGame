namespace ULinkGame.Server.Hotfix.Loading;

public sealed class VersionPointerHotfixAssemblySource : IHotfixAssemblySource
{
    private readonly string _rootDirectory;
    private readonly string _pointerFileName;
    private readonly string _assemblyFileName;

    public VersionPointerHotfixAssemblySource(string rootDirectory, string pointerFileName, string assemblyFileName)
    {
        _rootDirectory = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
        _pointerFileName = pointerFileName ?? throw new ArgumentNullException(nameof(pointerFileName));
        _assemblyFileName = assemblyFileName ?? throw new ArgumentNullException(nameof(assemblyFileName));
    }

    public async ValueTask<HotfixAssemblySourceResult> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(_rootDirectory);
        var pointerPath = Path.Combine(root, _pointerFileName);
        var version = (await File.ReadAllTextAsync(pointerPath, cancellationToken).ConfigureAwait(false)).Trim();
        if (string.IsNullOrWhiteSpace(version) || version.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException($"Hotfix version pointer '{pointerPath}' contains an invalid version.");
        }

        var versionDirectory = Path.Combine(root, "versions", version);
        var assemblyPath = Path.Combine(versionDirectory, _assemblyFileName);
        return new HotfixAssemblySourceResult("version-pointer", version, assemblyPath, versionDirectory);
    }
}
