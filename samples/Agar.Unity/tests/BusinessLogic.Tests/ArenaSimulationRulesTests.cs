using Shared.Gameplay;
using Shared.Interfaces;
using Xunit;

namespace Agar.Unity.Tests;

public sealed class ArenaSimulationRulesTests
{
    [Fact]
    public void MaxRoundSecondsZeroDisablesTimerBasedMatchEnd()
    {
        var simulation = new ArenaSimulation(new ArenaSimulationOptions
        {
            EnableBots = false,
            FoodTargetCount = 0,
            MinPlayersToStart = 1,
            TargetParticipantCount = 1,
            MaxRoundSeconds = 0f
        });

        simulation.UpsertPlayer(new ArenaPlayerRegistration
        {
            PlayerId = "Player",
            Score = 1
        });

        ArenaStepResult result = null!;
        for (var i = 0; i < 400; i++)
        {
            result = simulation.Tick(0.05f);
        }

        Assert.Null(result.MatchEnd);
        Assert.Equal(0, result.WorldState.RoundRemainingSeconds);
    }

    [Fact]
    public void HighStartingScoreControlsStartingScoreAndMass()
    {
        var simulation = new ArenaSimulation(new ArenaSimulationOptions
        {
            EnableBots = false,
            FoodTargetCount = 0,
            MinPlayersToStart = 1,
            TargetParticipantCount = 1,
            MaxRoundSeconds = 0f
        });

        simulation.UpsertPlayer(new ArenaPlayerRegistration
        {
            PlayerId = "Player",
            Score = 1000
        });

        var player = Assert.Single(simulation.CreateWorldState().Players);
        Assert.Equal(1000, player.Score);
        Assert.True(player.Mass > 24f);
    }

    [Fact]
    public void InvertedMoveSpeedPlayerGetsFasterAsMassGrows()
    {
        var small = CreateSinglePlayerState(score: 1, invertedSpeed: true);
        var large = CreateSinglePlayerState(score: 1000, invertedSpeed: true);

        Assert.True(large.MoveSpeed > small.MoveSpeed);
    }

    [Fact]
    public void NormalMoveSpeedPlayerGetsSlowerAsMassGrows()
    {
        var small = CreateSinglePlayerState(score: 1, invertedSpeed: false);
        var large = CreateSinglePlayerState(score: 1000, invertedSpeed: false);

        Assert.True(large.MoveSpeed < small.MoveSpeed);
    }

    [Fact]
    public void MoveSpeedMultiplierAppliesOnlyToConfiguredPlayer()
    {
        var simulation = new ArenaSimulation(new ArenaSimulationOptions
        {
            EnableBots = false,
            FoodTargetCount = 0,
            MinPlayersToStart = 1,
            TargetParticipantCount = 2,
            MaxRoundSeconds = 0f,
            MoveSpeedMultiplierPlayerId = "Boosted",
            MoveSpeedMultiplier = 2f
        });

        simulation.UpsertPlayer(new ArenaPlayerRegistration { PlayerId = "Boosted", Score = 1 });
        simulation.UpsertPlayer(new ArenaPlayerRegistration { PlayerId = "Normal", Score = 1 });

        var players = simulation.CreateWorldState().Players;
        var boosted = players.Single(player => player.PlayerId == "Boosted");
        var normal = players.Single(player => player.PlayerId == "Normal");

        Assert.Equal(normal.MoveSpeed * 2f, boosted.MoveSpeed, precision: 4);
    }

    [Fact]
    public void DefaultPickupTypesOnlyContainScorePoint()
    {
        var options = new ArenaSimulationOptions();

        var pickupType = Assert.Single(options.EnabledPickupTypes);
        Assert.Equal(PickupType.ScorePoint, pickupType);
    }

    [Fact]
    public void GeneratedFoodUsesOnlyScorePoint()
    {
        var simulation = new ArenaSimulation(new ArenaSimulationOptions
        {
            EnableBots = false,
            FoodTargetCount = 16,
            MinPlayersToStart = 1,
            TargetParticipantCount = 1,
            MaxRoundSeconds = 0f
        });

        var world = simulation.CreateWorldState();

        Assert.All(world.Pickups, pickup => Assert.Equal(PickupType.ScorePoint, pickup.Type));
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 7)]
    [InlineData(3, 5)]
    [InlineData(4, 3)]
    [InlineData(5, 1)]
    [InlineData(6, 0)]
    public void VictoryPointsMatchRankTable(int rank, int expectedPoints)
    {
        Assert.Equal(expectedPoints, VictoryPointAwards.GetPointsForRank(rank));
    }

    [Theory]
    [InlineData("AI01", true)]
    [InlineData("AI-bot", true)]
    [InlineData("Player", false)]
    public void VictoryPointBotFilterUsesAiPrefix(string playerId, bool expected)
    {
        Assert.Equal(expected, VictoryPointAwards.IsBotPlayer(playerId));
    }

    private static PlayerState CreateSinglePlayerState(int score, bool invertedSpeed)
    {
        var simulation = new ArenaSimulation(new ArenaSimulationOptions
        {
            EnableBots = false,
            FoodTargetCount = 0,
            MinPlayersToStart = 1,
            TargetParticipantCount = 1,
            MaxRoundSeconds = 0f,
            InvertedMoveSpeedPlayerId = invertedSpeed ? "Player" : null
        });

        simulation.UpsertPlayer(new ArenaPlayerRegistration
        {
            PlayerId = "Player",
            Score = score
        });

        return Assert.Single(simulation.CreateWorldState().Players);
    }
}
