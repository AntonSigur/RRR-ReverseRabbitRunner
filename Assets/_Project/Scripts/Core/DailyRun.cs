using System;
using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Daily-seeded run mode. Each calendar day (UTC) maps to a deterministic
    /// seed; players opting into Daily Mode see the same chunk layout, the
    /// same pickup placement, and the same per-day best score.
    ///
    /// State:
    /// - <see cref="IsActive"/> is a session-only toggle; resetting on quit
    ///   is intentional so the menu always defaults to a normal infinite run.
    /// - <see cref="TodaySeed"/> derives from the current UTC date — stable
    ///   for 24 hours.
    /// - The per-day best score lives in <see cref="PlayerPrefs"/> under a
    ///   date-keyed slot, so today's best is queryable in the UI.
    /// </summary>
    public static class DailyRun
    {
        public static bool IsActive { get; private set; }

        /// <summary>Stable seed for the current UTC date.</summary>
        public static int TodaySeed => DateSeed(DateTime.UtcNow);

        /// <summary>Human-readable label, e.g. "2024-04-26".</summary>
        public static string TodayLabel => DateTime.UtcNow.ToString("yyyy-MM-dd");

        public static event Action<bool> OnActiveChanged;

        public static void SetActive(bool active)
        {
            if (IsActive == active) return;
            IsActive = active;
            OnActiveChanged?.Invoke(active);
        }

        /// <summary>Best score recorded for today's seed.</summary>
        public static int TodayBestScore
        {
            get => PlayerPrefs.GetInt(BestScoreKey(TodayLabel), 0);
        }

        /// <summary>
        /// Submit a final score for today's seed. Updates per-day best when
        /// it exceeds the previous record. Returns true when a new record
        /// was set.
        /// </summary>
        public static bool SubmitScore(int score)
        {
            string key = BestScoreKey(TodayLabel);
            int current = PlayerPrefs.GetInt(key, 0);
            if (score > current)
            {
                PlayerPrefs.SetInt(key, score);
                PlayerPrefs.Save();
                return true;
            }
            return false;
        }

        // FNV-1a over the date string keeps the seed stable across reboots
        // and platforms — DateTime.GetHashCode() is randomised in modern
        // .NET builds, which would defeat the "shared seed for all players
        // today" goal.
        public static int DateSeed(DateTime utc)
        {
            string s = utc.ToString("yyyyMMdd");
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < s.Length; i++)
                {
                    hash ^= s[i];
                    hash *= 16777619u;
                }
                return (int)(hash & 0x7fffffff);
            }
        }

        private static string BestScoreKey(string dateLabel) => "DailyBest_" + dateLabel;
    }
}
