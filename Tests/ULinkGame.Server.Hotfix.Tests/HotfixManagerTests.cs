using ULinkGame.Server.Hotfix.Abstractions;
using ULinkGame.Server.Hotfix.Loading;
using Xunit;

namespace ULinkGame.Server.Hotfix.Tests;

public sealed class HotfixManagerTests
{
    [Fact]
    public async Task Reload_replaces_current_snapshot_after_successful_scan()
    {
        var source = new FixedAssemblySource(typeof(ManagerTestStateSystem).Assembly.Location);
        var manager = new HotfixManager(source);

        var result = await manager.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Equal(1, result.Current.DispatchTableVersion);
        Assert.Contains(result.Current.Methods, key => key.MethodName == "Add");
    }

    [Fact]
    public async Task Reload_failure_keeps_previous_snapshot()
    {
        var source = new SwitchableAssemblySource(typeof(ManagerTestStateSystem).Assembly.Location);
        var manager = new HotfixManager(source);
        var first = await manager.ReloadAsync(TestContext.Current.CancellationToken);
        source.Path = @"Z:\missing\Missing.Hotfix.dll";

        var second = await manager.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.True(first.Succeeded);
        Assert.False(second.Succeeded);
        Assert.Equal(first.Current.DispatchTableVersion, second.Current.DispatchTableVersion);
    }

    private sealed class FixedAssemblySource : IHotfixAssemblySource
    {
        private readonly string _path;

        public FixedAssemblySource(string path)
        {
            _path = path;
        }

        public ValueTask<HotfixAssemblySourceResult> ResolveAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new HotfixAssemblySourceResult(
                "fixed",
                "test",
                _path,
                Path.GetDirectoryName(_path)!));
        }
    }

    private sealed class SwitchableAssemblySource : IHotfixAssemblySource
    {
        public SwitchableAssemblySource(string path)
        {
            Path = path;
        }

        public string Path { get; set; }

        public ValueTask<HotfixAssemblySourceResult> ResolveAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new HotfixAssemblySourceResult(
                "switchable",
                "test",
                Path,
                System.IO.Path.GetDirectoryName(Path) ?? Environment.CurrentDirectory));
        }
    }
}

public sealed class ManagerTestState
{
}

[HotfixSystemOf(typeof(ManagerTestState))]
public static class ManagerTestStateSystem
{
    public static int Add(this ManagerTestState state, int value)
    {
        return value;
    }
}
