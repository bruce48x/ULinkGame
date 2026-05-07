#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Shared.Interfaces;
using UnityEngine;

namespace SampleClient.Gameplay
{
    internal static partial class DotArenaMetaProgression
    {
        public static DotArenaTaskReadySummary GetClaimableTaskSummary(DotArenaMetaState? state)
        {
            if (state == null)
            {
                return new DotArenaTaskReadySummary();
            }

            NormalizeState(state, state.PlayerId);

            var dailyClaimable = 0;
            foreach (var task in state.DailyTasks)
            {
                if (!task.Claimed && task.Progress >= task.Target)
                {
                    dailyClaimable += 1;
                }
            }

            var newPlayerClaimable = 0;
            foreach (var task in state.NewPlayerTasks)
            {
                if (!task.Claimed && task.Progress >= task.Target)
                {
                    newPlayerClaimable += 1;
                }
            }

            return new DotArenaTaskReadySummary
            {
                DailyClaimableCount = dailyClaimable,
                NewPlayerClaimableCount = newPlayerClaimable,
                TotalClaimableCount = dailyClaimable + newPlayerClaimable
            };
        }

        public static int GetClaimableTaskCount(DotArenaMetaState? state)
        {
            return GetClaimableTaskSummary(state).TotalClaimableCount;
        }

        public static bool HasClaimableTasks(DotArenaMetaState? state)
        {
            return GetClaimableTaskCount(state) > 0;
        }

        public static bool TryGetRecentMatchSummary(DotArenaMetaState? state, out DotArenaRecentMatchSummary summary)
        {
            summary = GetRecentMatchSummary(state);
            return summary.HasRecord;
        }

        public static DotArenaRecentMatchSummary GetRecentMatchSummary(DotArenaMetaState? state)
        {
            if (state == null)
            {
                return new DotArenaRecentMatchSummary();
            }

            NormalizeState(state, state.PlayerId);
            if (state.MatchHistory.Count == 0)
            {
                return new DotArenaRecentMatchSummary();
            }

            var record = state.MatchHistory[0];
            return new DotArenaRecentMatchSummary
            {
                HasRecord = true,
                Mode = record.Mode,
                Result = record.Result,
                Score = record.Score,
                WinnerPlayerId = record.WinnerPlayerId,
                PlayedAtUtcIso = record.PlayedAtUtcIso
            };
        }

        public static DotArenaRecentMatchTrendSummary GetRecentMatchTrendSummary(DotArenaMetaState? state, int sampleCount = 5)
        {
            if (state == null)
            {
                return new DotArenaRecentMatchTrendSummary();
            }

            NormalizeState(state, state.PlayerId);
            if (state.MatchHistory.Count == 0)
            {
                return new DotArenaRecentMatchTrendSummary();
            }

            sampleCount = Math.Clamp(sampleCount, 1, 10);
            var historyCount = Math.Min(sampleCount, state.MatchHistory.Count);
            var wins = 0;
            var losses = 0;
            var scoreSum = 0;
            var bestScore = int.MinValue;
            var form = new List<string>(historyCount);

            for (var i = 0; i < historyCount; i++)
            {
                var record = state.MatchHistory[i];
                var won = string.Equals(record.Result, "Win", StringComparison.OrdinalIgnoreCase);
                form.Add(won ? "W" : "L");
                if (won)
                {
                    wins += 1;
                }
                else
                {
                    losses += 1;
                }

                scoreSum += record.Score;
                if (record.Score > bestScore)
                {
                    bestScore = record.Score;
                }
            }

            var currentStreak = 0;
            var currentStreakType = string.Empty;
            var first = state.MatchHistory[0];
            var leadingWon = string.Equals(first.Result, "Win", StringComparison.OrdinalIgnoreCase);
            for (var i = 0; i < historyCount; i++)
            {
                var record = state.MatchHistory[i];
                var won = string.Equals(record.Result, "Win", StringComparison.OrdinalIgnoreCase);
                if (i == 0)
                {
                    currentStreakType = won ? "Win" : "Loss";
                }

                if (won != leadingWon)
                {
                    break;
                }

                currentStreak += 1;
            }

            var trendLabel = wins > losses
                ? "Hot"
                : losses > wins
                    ? "Cold"
                    : "Balanced";

            return new DotArenaRecentMatchTrendSummary
            {
                HasHistory = true,
                SampleCount = historyCount,
                WinCount = wins,
                LossCount = losses,
                WinRate = historyCount == 0 ? 0f : wins / (float)historyCount,
                AverageScore = historyCount == 0 ? 0 : Mathf.RoundToInt(scoreSum / (float)historyCount),
                BestScore = bestScore < 0 ? 0 : bestScore,
                CurrentStreak = currentStreak,
                CurrentStreakType = currentStreakType,
                TrendLabel = trendLabel,
                FormStrip = string.Join("-", form)
            };
        }

        public static DotArenaLeaderboardSummary GetLeaderboardSummary(DotArenaMetaState? state)
        {
            if (state == null)
            {
                return new DotArenaLeaderboardSummary();
            }

            NormalizeState(state, state.PlayerId);

            var trend = GetRecentMatchTrendSummary(state, 5);
            var totalMatches = Math.Max(0, state.TotalMatches);
            var winRate = totalMatches == 0 ? 0f : state.TotalWins / (float)totalMatches;
            var entries = new List<DotArenaLeaderboardEntrySummary>(state.LeaderboardEntries);
            var localEntry = entries.Find(entry => entry.IsLocalPlayer);
            var rankLine = localEntry != null
                ? $"Global rank: #{localEntry.Position} | VP: {localEntry.VictoryPoints} | Trend: {trend.TrendLabel}"
                : $"Global rank: Unranked | Win rate: {winRate:P0} | Trend: {trend.TrendLabel}";
            var resetLine = state.LeaderboardSecondsUntilReset > 0
                ? $"Weekly reset in {FormatLeaderboardReset(state.LeaderboardSecondsUntilReset)}"
                : "Weekly reset pending next server query";

            return new DotArenaLeaderboardSummary
            {
                HasProfile = true,
                Title = string.IsNullOrWhiteSpace(state.LeaderboardPeriodStartUtc)
                    ? "Global Leaderboard"
                    : $"Global Leaderboard ({state.LeaderboardPeriodStartUtc})",
                PlayerLine = $"Player: {state.PlayerId} | Wins: {state.TotalWins} | Matches: {state.TotalMatches} | Win rate: {winRate:P0}",
                RankLine = rankLine,
                TrendLine = trend.HasHistory
                    ? $"Recent form: {trend.FormStrip} | {trend.CurrentStreakType} streak: {trend.CurrentStreak} | Avg score: {trend.AverageScore}"
                    : "Recent form: No history",
                FormLine = resetLine + " | " + (trend.HasHistory
                    ? $"Last {trend.SampleCount}: {trend.WinCount}W / {trend.LossCount}L | Best score: {trend.BestScore}"
                    : "Last 0: no matches yet"),
                Entries = entries
            };
        }

        public static void ApplyLeaderboard(DotArenaMetaState? state, LeaderboardReply reply)
        {
            if (state == null || reply.Code != 0)
            {
                return;
            }

            NormalizeState(state, state.PlayerId);
            state.LeaderboardPeriodStartUtc = reply.PeriodStartUtc;
            state.LeaderboardSecondsUntilReset = Math.Max(0, reply.SecondsUntilReset);
            state.LeaderboardEntries.Clear();
            foreach (var entry in reply.Entries)
            {
                state.LeaderboardEntries.Add(new DotArenaLeaderboardEntrySummary
                {
                    Position = entry.Rank,
                    Name = entry.PlayerId,
                    VictoryPoints = entry.VictoryPoints,
                    Wins = entry.WinCount,
                    Matches = 0,
                    Note = "Server",
                    IsLocalPlayer = string.Equals(entry.PlayerId, state.PlayerId, StringComparison.Ordinal)
                });
            }
        }

        private static string FormatLeaderboardReset(int seconds)
        {
            var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
            if (span.TotalDays >= 1d)
            {
                return $"{(int)span.TotalDays}d {span.Hours}h";
            }

            if (span.TotalHours >= 1d)
            {
                return $"{(int)span.TotalHours}h {span.Minutes}m";
            }

            return $"{span.Minutes}m";
        }

        public static DotArenaShopAvailabilitySummary GetShopAvailabilitySummary(DotArenaMetaState? state)
        {
            if (state == null)
            {
                return new DotArenaShopAvailabilitySummary();
            }

            NormalizeState(state, state.PlayerId);

            var owned = 0;
            var affordable = 0;
            var affordableAndUnowned = 0;
            DotArenaShopItem? cheapestAffordable = null;
            DotArenaShopItem? cheapestAffordableUnowned = null;

            foreach (var item in ShopCatalog)
            {
                var isOwned = state.OwnedCosmeticIds != null && state.OwnedCosmeticIds.Contains(item.Id);
                if (isOwned)
                {
                    owned += 1;
                }

                if (state.SoftCurrency < item.Price)
                {
                    continue;
                }

                affordable += 1;
                if (!isOwned)
                {
                    affordableAndUnowned += 1;
                }

                if (cheapestAffordable == null || item.Price < cheapestAffordable.Price)
                {
                    cheapestAffordable = item;
                }

                if (!isOwned && (cheapestAffordableUnowned == null || item.Price < cheapestAffordableUnowned.Price))
                {
                    cheapestAffordableUnowned = item;
                }
            }

            return new DotArenaShopAvailabilitySummary
            {
                TotalCatalogCount = ShopCatalog.Length,
                OwnedCount = owned,
                AffordableCount = affordable,
                AffordableAndUnownedCount = affordableAndUnowned,
                CheapestAffordableItem = cheapestAffordable,
                CheapestAffordableUnownedItem = cheapestAffordableUnowned
            };
        }

        public static int GetPurchasableShopItemCount(DotArenaMetaState? state)
        {
            return GetShopAvailabilitySummary(state).AffordableAndUnownedCount;
        }

        public static bool TryGetCheapestPurchasableShopItem(DotArenaMetaState? state, out DotArenaShopItem? item)
        {
            var summary = GetShopAvailabilitySummary(state);
            item = summary.CheapestAffordableUnownedItem;
            return item != null;
        }
    }
}
