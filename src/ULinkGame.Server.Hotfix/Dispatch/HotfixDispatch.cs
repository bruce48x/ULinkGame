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
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(parameterTypes);

        if (parameterTypes.Any(static type => type is null))
        {
            throw new ArgumentException("Parameter types cannot contain null.", nameof(parameterTypes));
        }

        return new HotfixMethodKey(
            typeof(TState).FullName ?? typeof(TState).Name,
            methodName,
            typeof(TResult).FullName ?? typeof(TResult).Name,
            parameterTypes.Select(static type => type.FullName ?? type.Name).ToArray());
    }

    public static TResult Invoke<TState, TResult>(
        string methodName,
        TState state,
        Type[] parameterTypes,
        object?[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(parameterTypes);
        ArgumentNullException.ThrowIfNull(arguments);

        if (parameterTypes.Length != arguments.Length)
        {
            throw new ArgumentException(
                "Parameter type count must match argument count.",
                nameof(arguments));
        }

        if (parameterTypes.Any(static type => type is null))
        {
            throw new ArgumentException("Parameter types cannot contain null.", nameof(parameterTypes));
        }

        var table = Current;
        var key = CreateKey<TState, TResult>(methodName, parameterTypes);
        var method = table.Resolve(key);
        var invokeArguments = new object?[arguments.Length + 1];
        invokeArguments[0] = state;
        Array.Copy(arguments, 0, invokeArguments, 1, arguments.Length);

        var result = method.Invoke(null, invokeArguments);
        if (result is TResult typedResult)
        {
            return typedResult;
        }

        if (result is null && default(TResult) is null)
        {
            return default!;
        }

        throw new InvalidOperationException($"Hotfix method '{key}' returned an invalid result.");
    }
}
