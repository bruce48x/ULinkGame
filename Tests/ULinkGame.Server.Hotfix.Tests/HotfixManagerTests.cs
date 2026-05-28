using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ULinkGame.Server.Hotfix.Abstractions;
using ULinkGame.Server.Hotfix.Dispatch;
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

    [Fact]
    public async Task Reload_shares_configured_stable_assemblies_from_default_context()
    {
        using var compiled = await CompiledHotfixFixture.CreateAsync(TestContext.Current.CancellationToken);
        var stableAssembly = Assembly.LoadFrom(compiled.StableAssemblyPath);
        var source = new FixedAssemblySource(compiled.HotfixAssemblyPath);
        var manager = new HotfixManager(source, [stableAssembly.GetName().Name!]);

        var result = await manager.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics));
        var method = HotfixDispatch.Current.Resolve(result.Current.Methods.Single());
        Assert.Same(stableAssembly, method.GetParameters()[0].ParameterType.Assembly);
    }

    [Fact]
    public async Task Reload_does_not_replace_dispatch_after_scan_failure()
    {
        using var compiled = await CompiledHotfixFixture.CreateAsync(TestContext.Current.CancellationToken);
        var source = new SwitchableAssemblySource(typeof(ManagerTestStateSystem).Assembly.Location);
        var manager = new HotfixManager(source);
        var first = await manager.ReloadAsync(TestContext.Current.CancellationToken);
        var key = first.Current.Methods.Single(key => key.MethodName == "Add");
        var previousMethod = HotfixDispatch.Current.Resolve(key);
        source.Path = compiled.InvalidHotfixAssemblyPath;

        var second = await manager.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.False(second.Succeeded);
        Assert.Equal(first.Current.DispatchTableVersion, second.Current.DispatchTableVersion);
        Assert.Same(previousMethod, HotfixDispatch.Current.Resolve(key));
    }

    [Fact]
    public async Task Reload_propagates_cancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var manager = new HotfixManager(new CanceledAssemblySource());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await manager.ReloadAsync(cts.Token));
    }

    [Fact]
    public async Task ReloadAsync_serializes_concurrent_reloads()
    {
        var source = new BlockingAssemblySource(typeof(ManagerTestStateSystem).Assembly.Location);
        var manager = new HotfixManager(source);
        var first = manager.ReloadAsync(TestContext.Current.CancellationToken).AsTask();
        await source.FirstResolveStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        var second = manager.ReloadAsync(TestContext.Current.CancellationToken).AsTask();
        await Task.Yield();

        Assert.Equal(1, source.ResolveStarts);
        source.AllowFirstResolve.SetResult();
        await first.WaitAsync(TestContext.Current.CancellationToken);
        await source.SecondResolveStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        source.AllowSecondResolve.SetResult();
        await second.WaitAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(@"..\Hotfix.dll")]
    [InlineData(@"nested\Hotfix.dll")]
    [InlineData(@"/tmp/Hotfix.dll")]
    public async Task CurrentDirectorySource_rejects_unsafe_assembly_file_names(string assemblyFileName)
    {
        var source = new CurrentDirectoryHotfixAssemblySource(Environment.CurrentDirectory, assemblyFileName);

        await Assert.ThrowsAsync<ArgumentException>(async () => await source.ResolveAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(@"..\current.txt", "Hotfix.dll")]
    [InlineData(@"nested\current.txt", "Hotfix.dll")]
    [InlineData("current.txt", @"..\Hotfix.dll")]
    [InlineData("current.txt", @"nested\Hotfix.dll")]
    [InlineData("current.txt", @"/tmp/Hotfix.dll")]
    public async Task VersionPointerSource_rejects_unsafe_file_names(string pointerFileName, string assemblyFileName)
    {
        var source = new VersionPointerHotfixAssemblySource(Environment.CurrentDirectory, pointerFileName, assemblyFileName);

        await Assert.ThrowsAsync<ArgumentException>(async () => await source.ResolveAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AddULinkGameHotfix_replaces_existing_source_registration()
    {
        var oldSource = new FixedAssemblySource(typeof(ManagerTestStateSystem).Assembly.Location);
        var newSource = new FixedAssemblySource(typeof(ManagerTestStateSystem).Assembly.Location);
        var services = new ServiceCollection();
        services.AddSingleton<IHotfixAssemblySource>(oldSource);

        services.AddULinkGameHotfix(newSource);

        using var provider = services.BuildServiceProvider();
        Assert.Same(newSource, provider.GetRequiredService<IHotfixAssemblySource>());
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

    private sealed class CanceledAssemblySource : IHotfixAssemblySource
    {
        public ValueTask<HotfixAssemblySourceResult> ResolveAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromCanceled<HotfixAssemblySourceResult>(cancellationToken);
        }
    }

    private sealed class BlockingAssemblySource : IHotfixAssemblySource
    {
        private readonly string _path;
        private int _resolveStarts;

        public BlockingAssemblySource(string path)
        {
            _path = path;
        }

        public int ResolveStarts => Volatile.Read(ref _resolveStarts);

        public TaskCompletionSource FirstResolveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SecondResolveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AllowFirstResolve { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AllowSecondResolve { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<HotfixAssemblySourceResult> ResolveAsync(CancellationToken cancellationToken = default)
        {
            var start = Interlocked.Increment(ref _resolveStarts);
            if (start == 1)
            {
                FirstResolveStarted.SetResult();
                await AllowFirstResolve.Task.WaitAsync(cancellationToken);
            }
            else
            {
                SecondResolveStarted.SetResult();
                await AllowSecondResolve.Task.WaitAsync(cancellationToken);
            }

            return new HotfixAssemblySourceResult(
                "blocking",
                start.ToString(),
                _path,
                Path.GetDirectoryName(_path)!);
        }
    }

    private sealed class CompiledHotfixFixture : IDisposable
    {
        private CompiledHotfixFixture(
            string rootDirectory,
            string stableAssemblyPath,
            string hotfixAssemblyPath,
            string invalidHotfixAssemblyPath)
        {
            RootDirectory = rootDirectory;
            StableAssemblyPath = stableAssemblyPath;
            HotfixAssemblyPath = hotfixAssemblyPath;
            InvalidHotfixAssemblyPath = invalidHotfixAssemblyPath;
        }

        public string RootDirectory { get; }

        public string StableAssemblyPath { get; }

        public string HotfixAssemblyPath { get; }

        public string InvalidHotfixAssemblyPath { get; }

        public static async Task<CompiledHotfixFixture> CreateAsync(CancellationToken cancellationToken)
        {
            var root = Path.Combine(Path.GetTempPath(), "ULinkGameHotfixTests", Guid.NewGuid().ToString("N"));
            var stableProject = Path.Combine(root, "StableContracts", "StableContracts.csproj");
            var hotfixProject = Path.Combine(root, "HotfixLogic", "HotfixLogic.csproj");
            var invalidProject = Path.Combine(root, "InvalidHotfixLogic", "InvalidHotfixLogic.csproj");
            var abstractionsProject = FindRepositoryFile(Path.Combine("src", "ULinkGame.Server.Hotfix.Abstractions", "ULinkGame.Server.Hotfix.Abstractions.csproj"));

            Directory.CreateDirectory(Path.GetDirectoryName(stableProject)!);
            Directory.CreateDirectory(Path.GetDirectoryName(hotfixProject)!);
            Directory.CreateDirectory(Path.GetDirectoryName(invalidProject)!);

            await File.WriteAllTextAsync(
                stableProject,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>
                </Project>
                """,
                cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(Path.GetDirectoryName(stableProject)!, "ArenaSimulation.cs"),
                """
                namespace StableContracts;

                public sealed class ArenaSimulation
                {
                }
                """,
                cancellationToken);

            var hotfixProjectContent =
                $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include="{{stableProject}}" />
                    <ProjectReference Include="{{abstractionsProject}}" />
                  </ItemGroup>
                </Project>
                """;
            await File.WriteAllTextAsync(hotfixProject, hotfixProjectContent, cancellationToken);
            await File.WriteAllTextAsync(invalidProject, hotfixProjectContent, cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(Path.GetDirectoryName(hotfixProject)!, "ArenaSimulationSystem.cs"),
                """
                using StableContracts;
                using ULinkGame.Server.Hotfix.Abstractions;

                namespace HotfixLogic;

                [HotfixSystemOf(typeof(ArenaSimulation))]
                public static class ArenaSimulationSystem
                {
                    public static int Tick(this ArenaSimulation self, int delta)
                    {
                        return delta;
                    }
                }
                """,
                cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(Path.GetDirectoryName(invalidProject)!, "ArenaSimulationSystem.cs"),
                """
                using StableContracts;
                using ULinkGame.Server.Hotfix.Abstractions;

                namespace InvalidHotfixLogic;

                [HotfixSystemOf(typeof(ArenaSimulation))]
                public static class ArenaSimulationSystem
                {
                    public static bool TryRead(this ArenaSimulation self, out int value)
                    {
                        value = 0;
                        return true;
                    }
                }
                """,
                cancellationToken);

            await RunDotnetBuildAsync(stableProject, cancellationToken);
            await RunDotnetBuildAsync(hotfixProject, cancellationToken);
            await RunDotnetBuildAsync(invalidProject, cancellationToken);

            return new CompiledHotfixFixture(
                root,
                Path.Combine(Path.GetDirectoryName(stableProject)!, "bin", "Debug", "net10.0", "StableContracts.dll"),
                Path.Combine(Path.GetDirectoryName(hotfixProject)!, "bin", "Debug", "net10.0", "HotfixLogic.dll"),
                Path.Combine(Path.GetDirectoryName(invalidProject)!, "bin", "Debug", "net10.0", "InvalidHotfixLogic.dll"));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(RootDirectory, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string FindRepositoryFile(string relativePath)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new FileNotFoundException($"Could not find repository file '{relativePath}'.");
        }

        private static async Task RunDotnetBuildAsync(string projectPath, CancellationToken cancellationToken)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList = { "build", projectPath },
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }) ?? throw new InvalidOperationException("Could not start dotnet build.");

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"dotnet build failed for '{projectPath}'.{Environment.NewLine}{output}{Environment.NewLine}{error}");
            }
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
