using System.Reflection;
using ULinkGame.Server.Hotfix.Abstractions;

namespace ULinkGame.Server.Hotfix.Dispatch;

public sealed class HotfixDispatchTable
{
    private readonly IReadOnlyDictionary<HotfixMethodKey, MethodInfo> methods;

    public HotfixDispatchTable(long version, IEnumerable<HotfixMethodBinding> methods)
    {
        ArgumentNullException.ThrowIfNull(methods);

        var methodList = new List<HotfixMethodBinding>();
        foreach (var method in methods)
        {
            if (method is null)
            {
                throw new ArgumentException("Method bindings cannot contain null.", nameof(methods));
            }

            methodList.Add(method);
        }

        Version = version;
        this.methods = methodList.ToDictionary(static method => method.Key, static method => method.Method);
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
