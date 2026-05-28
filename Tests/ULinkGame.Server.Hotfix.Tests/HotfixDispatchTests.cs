using ULinkGame.Server.Hotfix.Abstractions;
using ULinkGame.Server.Hotfix.Dispatch;
using ULinkGame.Server.Hotfix.Scanning;
using Xunit;

namespace ULinkGame.Server.Hotfix.Tests;

public sealed class HotfixDispatchTests
{
    [Fact]
    public void Invoke_calls_loaded_static_extension_method()
    {
        var scan = HotfixSystemScanner.Scan(typeof(DispatchTestStateSystem).Assembly);
        HotfixDispatch.Replace(new HotfixDispatchTable(1, scan.Methods));
        var state = new DispatchTestState { Value = 5 };

        var result = HotfixDispatch.Invoke<DispatchTestState, int>(
            "Add",
            state,
            [typeof(int)],
            [7]);

        Assert.Equal(12, result);
    }

    [Fact]
    public void Invoke_calls_loaded_void_static_extension_method()
    {
        var scan = HotfixSystemScanner.Scan(typeof(DispatchTestStateSystem).Assembly);
        HotfixDispatch.Replace(new HotfixDispatchTable(1, scan.Methods));
        var state = new DispatchTestState { Value = 5 };

        HotfixDispatch.Invoke(
            "AddExp",
            state,
            [typeof(int)],
            [7]);

        Assert.Equal(12, state.Value);
    }
}

public sealed class DispatchTestState
{
    public int Value { get; set; }
}

[HotfixSystemOf(typeof(DispatchTestState))]
public static class DispatchTestStateSystem
{
    public static int Add(this DispatchTestState self, int amount)
    {
        return self.Value + amount;
    }

    public static void AddExp(this DispatchTestState self, int amount)
    {
        self.Value += amount;
    }
}
