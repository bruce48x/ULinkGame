namespace ULinkGame.Server.Hotfix.Loading;

public sealed class CurrentDirectoryHotfixAssemblySource : IHotfixAssemblySource
{
    private readonly string _directory;
    private readonly string _assemblyFileName;

    public CurrentDirectoryHotfixAssemblySource(string directory, string assemblyFileName)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _assemblyFileName = assemblyFileName ?? throw new ArgumentNullException(nameof(assemblyFileName));
    }

    public ValueTask<HotfixAssemblySourceResult> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var fullDirectory = Path.GetFullPath(_directory);
        var assemblyPath = Path.Combine(fullDirectory, _assemblyFileName);
        return ValueTask.FromResult(new HotfixAssemblySourceResult("current-directory", null, assemblyPath, fullDirectory));
    }
}
