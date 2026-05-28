using Shared.Gameplay;
using ULinkGame.Server.Hotfix.Dispatch;
using ULinkGame.Server.Hotfix.Scanning;
using Xunit;

namespace Agar.Unity.Tests;

public sealed class AgarHotfixTests
{
    [Fact]
    public void SettleMatch_uses_hotfix_rule_to_award_winner_points()
    {
        var scan = HotfixSystemScanner.Scan(typeof(Agar.Sample.Hotfix.Gameplay.ArenaSettlementSystem).Assembly);
        Assert.Empty(scan.Diagnostics);
        HotfixDispatch.Replace(new HotfixDispatchTable(1, scan.Methods));

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
}
