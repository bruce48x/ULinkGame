using Orleans;
using Orleans.Contracts.Leaderboard;
using Orleans.Contracts.Users;
using Orleans.Runtime;

namespace ULinkRPC.Sample.Silo.Leaderboard;

[GenerateSerializer]
public sealed class LeaderboardState
{
    [Id(0)]
    public string CurrentPeriodStartUtc { get; set; } = "";

    [Id(1)]
    public Dictionary<string, LeaderboardPlayerState> Players { get; set; } = new(StringComparer.Ordinal);

    [Id(2)]
    public List<WeeklyLeaderboardSnapshot> WeeklySnapshots { get; set; } = new();
}

[GenerateSerializer]
public sealed class LeaderboardPlayerState
{
    [Id(0)]
    public string PlayerId { get; set; } = "";

    [Id(1)]
    public int VictoryPoints { get; set; }

    [Id(2)]
    public int WinCount { get; set; }
}

[GenerateSerializer]
public sealed class WeeklyLeaderboardSnapshot
{
    [Id(0)]
    public string PeriodStartUtc { get; set; } = "";

    [Id(1)]
    public List<LeaderboardEntrySnapshot> Entries { get; set; } = new();
}

public sealed class LeaderboardGrain : Grain, ILeaderboardGrain
{
    private readonly IPersistentState<LeaderboardState> _state;

    public LeaderboardGrain([PersistentState("leaderboard", "leaderboards")] IPersistentState<LeaderboardState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        EnsurePeriodInitialized(DateTime.UtcNow);
    }

    public async Task<LeaderboardSnapshot> GetLeaderboardAsync(int topN)
    {
        await ResetWeeklyIfNeededAsync();

        topN = Math.Clamp(topN, 1, 100);
        var now = DateTime.UtcNow;
        var entries = GetRankedEntries()
            .Take(topN)
            .ToList();

        return new LeaderboardSnapshot
        {
            PeriodStartUtc = _state.State.CurrentPeriodStartUtc,
            SecondsUntilReset = Math.Max(0, (int)Math.Ceiling((GetNextPeriodStart(now) - now).TotalSeconds)),
            Entries = entries
        };
    }

    public async Task ResetWeeklyIfNeededAsync()
    {
        var now = DateTime.UtcNow;
        EnsurePeriodInitialized(now);
        var currentPeriod = GetCurrentPeriodStart(now);
        if (string.Equals(_state.State.CurrentPeriodStartUtc, FormatPeriod(currentPeriod), StringComparison.Ordinal))
        {
            return;
        }

        var archived = new WeeklyLeaderboardSnapshot
        {
            PeriodStartUtc = _state.State.CurrentPeriodStartUtc,
            Entries = GetRankedEntries().Take(100).ToList()
        };

        if (archived.Entries.Count > 0)
        {
            _state.State.WeeklySnapshots.Insert(0, archived);
            if (_state.State.WeeklySnapshots.Count > 2)
            {
                _state.State.WeeklySnapshots.RemoveRange(2, _state.State.WeeklySnapshots.Count - 2);
            }
        }

        var playerIds = _state.State.Players.Keys.ToArray();
        foreach (var playerId in playerIds)
        {
            await GrainFactory.GetGrain<IUserGrain>(playerId).ResetVictoryPointsAsync();
        }

        _state.State.Players.Clear();
        _state.State.CurrentPeriodStartUtc = FormatPeriod(currentPeriod);
        await _state.WriteStateAsync();
    }

    public async Task RecordVictoryPointsAsync(string playerId, int victoryPoints, int winCount)
    {
        if (string.IsNullOrWhiteSpace(playerId) || victoryPoints <= 0)
        {
            return;
        }

        await ResetWeeklyIfNeededAsync();

        if (!_state.State.Players.TryGetValue(playerId, out var player))
        {
            player = new LeaderboardPlayerState { PlayerId = playerId };
            _state.State.Players[playerId] = player;
        }

        player.VictoryPoints = Math.Max(0, victoryPoints);
        player.WinCount = Math.Max(0, winCount);
        await _state.WriteStateAsync();
    }

    private List<LeaderboardEntrySnapshot> GetRankedEntries()
    {
        return _state.State.Players.Values
            .Where(static player => player.VictoryPoints > 0)
            .OrderByDescending(static player => player.VictoryPoints)
            .ThenByDescending(static player => player.WinCount)
            .ThenBy(static player => player.PlayerId, StringComparer.Ordinal)
            .Select((player, index) => new LeaderboardEntrySnapshot
            {
                PlayerId = player.PlayerId,
                VictoryPoints = player.VictoryPoints,
                WinCount = player.WinCount,
                Rank = index + 1
            })
            .ToList();
    }

    private void EnsurePeriodInitialized(DateTime now)
    {
        if (string.IsNullOrWhiteSpace(_state.State.CurrentPeriodStartUtc))
        {
            _state.State.CurrentPeriodStartUtc = FormatPeriod(GetCurrentPeriodStart(now));
        }
    }

    private static DateTime GetCurrentPeriodStart(DateTime utcNow)
    {
        var date = utcNow.Date;
        var daysSinceMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    private static DateTime GetNextPeriodStart(DateTime utcNow)
    {
        return GetCurrentPeriodStart(utcNow).AddDays(7);
    }

    private static string FormatPeriod(DateTime periodStartUtc)
    {
        return periodStartUtc.ToString("yyyy-MM-dd");
    }
}
