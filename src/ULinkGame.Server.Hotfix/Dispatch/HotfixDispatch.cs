using ULinkGame.Server.Hotfix.Abstractions;

namespace ULinkGame.Server.Hotfix.Dispatch;

public static class HotfixDispatch
{
    private static HotfixDispatchTable current = new(0, Array.Empty<HotfixMethodBinding>());

    public static HotfixDispatchTable Current => Volatile.Read(ref current);

    public static void Replace(HotfixDispatchTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        Interlocked.Exchange(ref current, table);
    }

    public static HotfixMethodKey CreateKey<TState, TResult>(string methodName, params Type[] parameterTypes)
    {
        return new HotfixMethodKey(
            typeof(TState).FullName ?? typeof(TState).Name,
            methodName,
            typeof(TResult).FullName ?? typeof(TResult).Name,
            parameterTypes.Select(static type => type.FullName ?? type.Name).ToArray());
    }
}
