using Shared.Gameplay;
using ULinkGame.Server.Hotfix;
using ULinkGame.Server.Hotfix.Dispatch;
using ULinkGame.Server.Hotfix.Loading;
using Xunit;

namespace Agar.Unity.Tests;

public sealed class AgarHotfixTests
{
    [Fact]
    public async Task SettleMatch_uses_hotfix_rule_to_award_winner_points()
    {
        var hotfixAssemblyPath = FindHotfixAssemblyPath();
        var source = new CurrentDirectoryHotfixAssemblySource(
            Path.GetDirectoryName(hotfixAssemblyPath)!,
            Path.GetFileName(hotfixAssemblyPath));
        var manager = new HotfixManager(source, [typeof(ArenaSimulation).Assembly.GetName().Name!]);

        var reload = await manager.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.True(reload.Succeeded, BuildReloadDiagnostics(reload));
        var settleMatchKey = Assert.Single(
            reload.Current.Methods,
            key => key.StateTypeName == typeof(ArenaSimulation).FullName &&
                   key.MethodName == nameof(ArenaSimulation.SettleMatch));
        var settleMatch = HotfixDispatch.Current.Resolve(settleMatchKey);
        Assert.Same(typeof(ArenaSimulation), settleMatch.GetParameters()[0].ParameterType);

        var simulation = new ArenaSimulation(new ArenaSimulationOptions
        {
            EnableBots = false,
            FoodTargetCount = 0
        });
        simulation.UpsertPlayer(new ArenaPlayerRegistration { PlayerId = "p1", Mass = 50 });
        simulation.UpsertPlayer(new ArenaPlayerRegistration { PlayerId = "p2", Mass = 25 });

        var settlement = simulation.SettleMatch(simulation.CreateWorldState());

        Assert.Equal("p1", settlement.WinnerPlayerId);
        Assert.Equal(10, settlement.Entries.Single(entry => entry.PlayerId == "p1").VictoryPoints);
    }

    private static string FindHotfixAssemblyPath()
    {
        const string assemblyFileName = "Agar.Sample.Hotfix.dll";

        var directCandidate = Path.Combine(AppContext.BaseDirectory, assemblyFileName);
        if (File.Exists(directCandidate))
        {
            return directCandidate;
        }

        var root = FindRepositoryRoot();
        var configuration = GetConfigurationName();
        var candidates = new[]
        {
            Path.Combine(root, "samples", "Agar.Unity", "Server", "Hotfix", "bin", configuration, "net10.0", assemblyFileName),
            Path.Combine(root, "samples", "Agar.Unity", "Server", "Hotfix", "bin", "Debug", "net10.0", assemblyFileName),
            Path.Combine(root, "samples", "Agar.Unity", "Server", "Hotfix", "bin", "Release", "net10.0", assemblyFileName)
        };

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"Could not locate {assemblyFileName}. Checked:{Environment.NewLine}{string.Join(Environment.NewLine, candidates.Prepend(directCandidate))}",
            assemblyFileName);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CONTRIBUTING.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "samples", "Agar.Unity")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not find repository root from '{AppContext.BaseDirectory}'.");
    }

    private static string GetConfigurationName()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    private static string BuildReloadDiagnostics(ULinkGame.Server.Hotfix.Abstractions.HotfixReloadResult reload)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                $"Status: {reload.Status}",
                $"RequestedPath: {reload.RequestedPath}",
                $"ErrorMessage: {reload.ErrorMessage}",
                $"ExceptionType: {reload.ExceptionType}",
                "Diagnostics:",
                string.Join(Environment.NewLine, reload.Diagnostics)
            });
    }
}
