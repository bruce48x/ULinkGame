using System.Reflection;
using System.Runtime.Loader;
using ULinkGame.Server.Hotfix.Abstractions;

namespace ULinkGame.Server.Hotfix.Loading;

internal sealed class HotfixAssemblyLoadContext : AssemblyLoadContext
{
    private static readonly string AbstractionsAssemblyName = typeof(HotfixSystemOfAttribute).Assembly.GetName().Name!;

    private readonly AssemblyDependencyResolver _resolver;

    public HotfixAssemblyLoadContext(string mainAssemblyPath)
        : base("ULinkGame.Hotfix", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name == AbstractionsAssemblyName)
        {
            return null;
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
