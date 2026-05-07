using Orleans;

namespace Orleans.Contracts.Leaderboard;

public interface ILeaderboardGrain : IGrainWithIntegerKey
{
    Task<LeaderboardSnapshot> GetLeaderboardAsync(int topN);
    Task ResetWeeklyIfNeededAsync();
    Task RecordVictoryPointsAsync(string playerId, int victoryPoints, int winCount);
}

[GenerateSerializer]
public sealed class LeaderboardSnapshot
{
    [Id(0)]
    public string PeriodStartUtc { get; set; } = "";

    [Id(1)]
    public int SecondsUntilReset { get; set; }

    [Id(2)]
    public List<LeaderboardEntrySnapshot> Entries { get; set; } = new();
}

[GenerateSerializer]
public sealed class LeaderboardEntrySnapshot
{
    [Id(0)]
    public string PlayerId { get; set; } = "";

    [Id(1)]
    public int VictoryPoints { get; set; }

    [Id(2)]
    public int WinCount { get; set; }

    [Id(3)]
    public int Rank { get; set; }
}
