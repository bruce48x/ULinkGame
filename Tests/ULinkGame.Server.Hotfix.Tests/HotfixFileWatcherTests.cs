using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ULinkGame.Server.Hotfix.Loading;
using Xunit;

namespace ULinkGame.Server.Hotfix.Tests;

public sealed class HotfixFileWatcherTests
{
    [Fact]
    public void Options_use_plan_defaults()
    {
        var options = new HotfixFileWatcherOptions();

        Assert.Equal("hotfix/current", options.Directory);
        Assert.Equal("*.dll", options.Filter);
        Assert.Equal(TimeSpan.FromSeconds(1), options.Debounce);
    }

    [Fact]
    public void AddULinkGameHotfixFileWatcher_applies_configured_options()
    {
        var services = new ServiceCollection();
        services.AddULinkGameHotfix(new FixedAssemblySource(typeof(HotfixFileWatcherTests).Assembly.Location));

        services.AddULinkGameHotfixFileWatcher(options =>
        {
            options.Directory = "custom/hotfix";
            options.Filter = "Game.*.dll";
            options.Debounce = TimeSpan.FromMilliseconds(250);
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HotfixFileWatcherOptions>>().Value;
        Assert.Equal("custom/hotfix", options.Directory);
        Assert.Equal("Game.*.dll", options.Filter);
        Assert.Equal(TimeSpan.FromMilliseconds(250), options.Debounce);
    }

    [Fact]
    public void AddULinkGameHotfixFileWatcher_registers_hosted_service()
    {
        var services = new ServiceCollection();
        services.AddULinkGameHotfix(new FixedAssemblySource(typeof(HotfixFileWatcherTests).Assembly.Location));
        services.AddSingleton<ILogger<HotfixFileWatcherHostedService>>(NullLogger<HotfixFileWatcherHostedService>.Instance);

        services.AddULinkGameHotfixFileWatcher();

        using var provider = services.BuildServiceProvider();
        var hostedService = Assert.Single(provider.GetServices<IHostedService>());
        Assert.IsType<HotfixFileWatcherHostedService>(hostedService);
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
}
