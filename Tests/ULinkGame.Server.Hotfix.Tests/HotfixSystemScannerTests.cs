using ULinkGame.Server.Hotfix.Abstractions;
using ULinkGame.Server.Hotfix.Scanning;
using Xunit;

namespace ULinkGame.Server.Hotfix.Tests;

public sealed class HotfixSystemScannerTests
{
    [Fact]
    public void Scan_discovers_public_static_extension_methods()
    {
        var result = HotfixSystemScanner.Scan(typeof(TestStateSystem).Assembly);

        var method = Assert.Single(result.Methods, method => method.Key.StateTypeName == typeof(TestState).FullName);
        Assert.Equal(typeof(TestState).FullName, method.Key.StateTypeName);
        Assert.Equal(nameof(TestStateSystem.Add), method.Key.MethodName);
        Assert.Equal(typeof(int).FullName, method.Key.ReturnTypeName);
        Assert.Equal([typeof(int).FullName!], method.Key.ParameterTypeNames);
    }

    [Fact]
    public void Scan_rejects_duplicate_method_keys()
    {
        var result = HotfixSystemScanner.Scan(typeof(DuplicateStateSystemA).Assembly);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("Duplicate hotfix method key", StringComparison.Ordinal));
    }

    public sealed class TestState
    {
        public int Value { get; set; }
    }

    public sealed class DuplicateState
    {
    }
}

[HotfixSystemOf(typeof(HotfixSystemScannerTests.TestState))]
public static class TestStateSystem
{
    public static int Add(this HotfixSystemScannerTests.TestState self, int amount)
    {
        return self.Value + amount;
    }
}

[HotfixSystemOf(typeof(HotfixSystemScannerTests.DuplicateState))]
public static class DuplicateStateSystemA
{
    public static int Add(this HotfixSystemScannerTests.DuplicateState self, int amount)
    {
        return amount;
    }
}

[HotfixSystemOf(typeof(HotfixSystemScannerTests.DuplicateState))]
public static class DuplicateStateSystemB
{
    public static int Add(this HotfixSystemScannerTests.DuplicateState self, int amount)
    {
        return amount + 1;
    }
}
