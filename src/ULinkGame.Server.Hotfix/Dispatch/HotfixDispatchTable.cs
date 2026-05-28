using System.Reflection;
using ULinkGame.Server.Hotfix.Abstractions;

namespace ULinkGame.Server.Hotfix.Dispatch;

public sealed class HotfixDispatchTable
{
    private readonly IReadOnlyDictionary<HotfixMethodKey, MethodInfo> methods;

    public HotfixDispatchTable(long version, IEnumerable<HotfixMethodBinding> methods)
    {
        Version = version;
        this.methods = methods.ToDictionary(static method => method.Key, static method => method.Method);
        MethodKeys = this.methods.Keys.OrderBy(static key => key.ToString(), StringComparer.Ordinal).ToArray();
    }

    public long Version { get; }

    public IReadOnlyList<HotfixMethodKey> MethodKeys { get; }

    public MethodInfo Resolve(HotfixMethodKey key)
    {
        return methods.TryGetValue(key, out var method)
            ? method
            : throw new MissingMethodException($"Hotfix method '{key}' is not loaded.");
    }
}
