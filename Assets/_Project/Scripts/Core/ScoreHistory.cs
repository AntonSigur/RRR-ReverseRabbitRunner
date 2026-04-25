using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Persistent local history of recent runs. Stored as a JSON array in
    /// PlayerPrefs so it survives builds and is human-inspectable.
    /// Capacity is fixed; oldest entries are evicted on overflow.
    /// </summary>
    public static class ScoreHistory
    {
        public const int Capacity = 20;
        private const string PrefsKey = "RunHistory";

        [Serializable]
        public struct Entry
        {
            public long unixSeconds;
            public int score;
            public int distance;
            public int carrots;
            public int maxCombo;
            public int maxMultiplier;
            public bool daily;
        }

        [Serializable]
        private class Wrapper
        {
            public List<Entry> entries = new List<Entry>();
        }

        private static Wrapper cache;

        public static event Action OnHistoryChanged;

        private static Wrapper Load()
        {
            if (cache != null) return cache;
            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                cache = new Wrapper();
                return cache;
            }
            try
            {
                cache = JsonUtility.FromJson<Wrapper>(json) ?? new Wrapper();
                if (cache.entries == null) cache.entries = new List<Entry>();
            }
            catch
            {
                cache = new Wrapper();
            }
            return cache;
        }

        private static void Save()
        {
            if (cache == null) return;
            string json = JsonUtility.ToJson(cache);
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
            OnHistoryChanged?.Invoke();
        }

        /// <summary>
        /// Append a run. Newest first. Trims to <see cref="Capacity"/>.
        /// </summary>
        public static void Submit(int score, float distance, int carrots,
            int maxCombo, int maxMultiplier, bool daily)
        {
            if (score <= 0 && distance <= 0f && carrots <= 0) return; // ignore empty runs
            var w = Load();
            var e = new Entry
            {
                unixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                score = score,
                distance = Mathf.RoundToInt(distance),
                carrots = carrots,
                maxCombo = maxCombo,
                maxMultiplier = maxMultiplier,
                daily = daily
            };
            w.entries.Insert(0, e);
            if (w.entries.Count > Capacity)
                w.entries.RemoveRange(Capacity, w.entries.Count - Capacity);
            Save();
        }

        /// <summary>Most-recent-first list of entries (returns the live list — do not mutate).</summary>
        public static IReadOnlyList<Entry> GetEntries() => Load().entries;

        public static int BestScore()
        {
            int best = 0;
            foreach (var e in Load().entries) if (e.score > best) best = e.score;
            return best;
        }

        public static int BestDistance()
        {
            int best = 0;
            foreach (var e in Load().entries) if (e.distance > best) best = e.distance;
            return best;
        }

        public static void Clear()
        {
            cache = new Wrapper();
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
            OnHistoryChanged?.Invoke();
        }

        /// <summary>"3m ago", "2h ago", "Yesterday", "12 Apr" — short relative label.</summary>
        public static string RelativeAge(long unixSeconds)
        {
            var then = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime;
            var now = DateTime.Now;
            var span = now - then;
            if (span.TotalSeconds < 60) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 2) return "yesterday";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            return then.ToString("d MMM");
        }
    }
}
