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

    [Id(3)]
    public string CurrentPeriodStartLocalDate { get; set; } = "";
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

    [Id(2)]
    public string PeriodStartLocalDate { get; set; } = "";
}

public sealed class LeaderboardGrain : Grain, ILeaderboardGrain
{
    private readonly IPersistentState<LeaderboardState> _state;
    private readonly TimeZoneInfo _leaderboardTimeZone;

    public LeaderboardGrain([PersistentState("leaderboard", "leaderboards")] IPersistentState<LeaderboardState> state)
    {
        _state = state;
        _leaderboardTimeZone = TimeZoneInfo.Local;
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

        var snapshot = new LeaderboardSnapshot
        {
            PeriodStartLocalDate = _state.State.CurrentPeriodStartLocalDate,
            PeriodStartUtc = _state.State.CurrentPeriodStartLocalDate,
            SecondsUntilReset = Math.Max(0, (int)Math.Ceiling((LeaderboardPeriodPolicy.GetNextPeriodStartUtc(now, _leaderboardTimeZone) - now).TotalSeconds)),
            Entries = entries
        };
        return snapshot;
    }

    public async Task ResetWeeklyIfNeededAsync()
    {
        var now = DateTime.UtcNow;
        EnsurePeriodInitialized(now);
        var currentPeriod = LeaderboardPeriodPolicy.GetCurrentPeriodStartLocalDate(now, _leaderboardTimeZone);
        if (string.Equals(_state.State.CurrentPeriodStartLocalDate, currentPeriod, StringComparison.Ordinal))
        {
            return;
        }

        var archived = new WeeklyLeaderboardSnapshot
        {
            PeriodStartLocalDate = _state.State.CurrentPeriodStartLocalDate,
            PeriodStartUtc = _state.State.CurrentPeriodStartLocalDate,
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
        _state.State.CurrentPeriodStartLocalDate = currentPeriod;
        _state.State.CurrentPeriodStartUtc = currentPeriod;
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
        return LeaderboardRankingPolicy.GetRankedEntries(_state.State.Players.Values);
    }

    private void EnsurePeriodInitialized(DateTime now)
    {
        if (string.IsNullOrWhiteSpace(_state.State.CurrentPeriodStartLocalDate)
            && !string.IsNullOrWhiteSpace(_state.State.CurrentPeriodStartUtc))
        {
            _state.State.CurrentPeriodStartLocalDate = LeaderboardPeriodPolicy.MigrateLegacyPeriodStartUtc(
                _state.State.CurrentPeriodStartUtc,
                now,
                _leaderboardTimeZone);
        }

        if (string.IsNullOrWhiteSpace(_state.State.CurrentPeriodStartLocalDate))
        {
            _state.State.CurrentPeriodStartLocalDate = LeaderboardPeriodPolicy.GetCurrentPeriodStartLocalDate(now, _leaderboardTimeZone);
        }

        _state.State.CurrentPeriodStartUtc = _state.State.CurrentPeriodStartLocalDate;
    }
}

public static class LeaderboardRankingPolicy
{
    public static List<LeaderboardEntrySnapshot> GetRankedEntries(IEnumerable<LeaderboardPlayerState> players)
    {
        return players
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
}

public static class LeaderboardPeriodPolicy
{
    public static string GetCurrentPeriodStartLocalDate(DateTime utcNow, TimeZoneInfo timeZone)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(NormalizeUtc(utcNow), timeZone);
        var localDate = localNow.Date;
        var daysSinceMonday = ((int)localDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return localDate.AddDays(-daysSinceMonday).ToString("yyyy-MM-dd");
    }

    public static DateTime GetNextPeriodStartUtc(DateTime utcNow, TimeZoneInfo timeZone)
    {
        var currentLocalStart = DateTime.ParseExact(
            GetCurrentPeriodStartLocalDate(utcNow, timeZone),
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None);
        var nextLocalStart = DateTime.SpecifyKind(currentLocalStart.AddDays(7), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(nextLocalStart, timeZone);
    }

    public static WeeklyLeaderboardSnapshot? ResetWeeklyIfNeeded(LeaderboardState state, DateTime utcNow, TimeZoneInfo timeZone)
    {
        EnsurePeriodInitialized(state, utcNow, timeZone);
        var currentPeriod = GetCurrentPeriodStartLocalDate(utcNow, timeZone);
        if (string.Equals(state.CurrentPeriodStartLocalDate, currentPeriod, StringComparison.Ordinal))
        {
            return null;
        }

        var archived = new WeeklyLeaderboardSnapshot
        {
            PeriodStartLocalDate = state.CurrentPeriodStartLocalDate,
            PeriodStartUtc = state.CurrentPeriodStartLocalDate,
            Entries = LeaderboardRankingPolicy.GetRankedEntries(state.Players.Values).Take(100).ToList()
        };

        if (archived.Entries.Count > 0)
        {
            state.WeeklySnapshots.Insert(0, archived);
            if (state.WeeklySnapshots.Count > 2)
            {
                state.WeeklySnapshots.RemoveRange(2, state.WeeklySnapshots.Count - 2);
            }
        }

        state.Players.Clear();
        state.CurrentPeriodStartLocalDate = currentPeriod;
        state.CurrentPeriodStartUtc = currentPeriod;
        return archived.Entries.Count > 0 ? archived : null;
    }

    private static void EnsurePeriodInitialized(LeaderboardState state, DateTime utcNow, TimeZoneInfo timeZone)
    {
        if (string.IsNullOrWhiteSpace(state.CurrentPeriodStartLocalDate)
            && !string.IsNullOrWhiteSpace(state.CurrentPeriodStartUtc))
        {
            state.CurrentPeriodStartLocalDate = MigrateLegacyPeriodStartUtc(state.CurrentPeriodStartUtc, utcNow, timeZone);
        }

        if (string.IsNullOrWhiteSpace(state.CurrentPeriodStartLocalDate))
        {
            state.CurrentPeriodStartLocalDate = GetCurrentPeriodStartLocalDate(utcNow, timeZone);
        }

        state.CurrentPeriodStartUtc = state.CurrentPeriodStartLocalDate;
    }

    private static DateTime NormalizeUtc(DateTime utcNow)
    {
        return utcNow.Kind == DateTimeKind.Utc
            ? utcNow
            : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    }

    public static string MigrateLegacyPeriodStartUtc(string legacyPeriodStartUtc, DateTime utcNow, TimeZoneInfo timeZone)
    {
        var normalizedUtc = NormalizeUtc(utcNow);
        var utcDate = normalizedUtc.Date;
        var daysSinceMonday = ((int)utcDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var currentUtcPeriodStart = utcDate.AddDays(-daysSinceMonday).ToString("yyyy-MM-dd");
        if (string.Equals(legacyPeriodStartUtc, currentUtcPeriodStart, StringComparison.Ordinal))
        {
            return GetCurrentPeriodStartLocalDate(normalizedUtc, timeZone);
        }

        return legacyPeriodStartUtc;
    }
}
