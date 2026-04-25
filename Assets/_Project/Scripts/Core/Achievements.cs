using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    public enum AchievementId
    {
        FirstHundred,        // run 100m total in a single run
        FirstKilometer,      // run 1000m total in a single run
        MarathonRunner,      // run 5000m total in a single run
        ComboNovice,         // hit a 5x combo
        ComboMaster,         // hit a 10x combo
        ComboLegend,         // hit a 25x combo
        TierClimber,         // reach difficulty tier 8
        TopHundredCarrots,   // collect 100 carrots lifetime
        TopThousandCarrots,  // collect 1000 carrots lifetime
        MagnetMagnet,        // trigger 25 magnet pickups lifetime
        NearMissPro,         // record 10 near-misses in a single run
        Untouchable,         // reach 500m without a single stumble
        FarFurtherFurthest,  // beat your best-distance record
        Pyromaniac,          // reach a score of 5000 in one run
        GoldenSecret,        // unlock the Golden Rabbit cheat
    }

    /// <summary>
    /// Static achievement registry: definitions, persistence, unlock state.
    ///
    /// Design:
    /// - Definitions live here so any subsystem can describe them in UI.
    /// - Unlock state and lifetime counters persist via <see cref="PlayerPrefs"/>.
    /// - Stateless w.r.t. game objects; <see cref="AchievementsRuntime"/> wires
    ///   the events.
    /// </summary>
    public static class Achievements
    {
        public readonly struct Definition
        {
            public readonly AchievementId Id;
            public readonly string Title;
            public readonly string Description;
            public readonly string Icon; // emoji glyph
            public Definition(AchievementId id, string title, string desc, string icon)
            {
                Id = id; Title = title; Description = desc; Icon = icon;
            }
        }

        private static readonly Definition[] DefinitionTable =
        {
            new(AchievementId.FirstHundred,       "Just a Sprint",        "Run 100 metres in one run.",                 "🏁"),
            new(AchievementId.FirstKilometer,     "Kilometre Club",       "Run 1 km in one run.",                       "🥕"),
            new(AchievementId.MarathonRunner,     "Marathon Rabbit",      "Run 5 km in one run.",                       "🏆"),
            new(AchievementId.ComboNovice,        "Combo Novice",         "Hit a 5× combo multiplier.",                 "✨"),
            new(AchievementId.ComboMaster,        "Combo Master",         "Hit a 10× combo multiplier.",                "💫"),
            new(AchievementId.ComboLegend,        "Combo Legend",         "Hit a 25× combo multiplier.",                "🌟"),
            new(AchievementId.TierClimber,        "Tier Climber",         "Reach difficulty tier 8.",                   "📈"),
            new(AchievementId.TopHundredCarrots,  "Carrot Connoisseur",   "Collect 100 carrots (lifetime).",            "🥕"),
            new(AchievementId.TopThousandCarrots, "Carrot Tycoon",        "Collect 1,000 carrots (lifetime).",          "💰"),
            new(AchievementId.MagnetMagnet,       "Magnet Magnet",        "Trigger 25 magnet power-ups (lifetime).",    "🧲"),
            new(AchievementId.NearMissPro,        "Lived Dangerously",    "10 near-misses in one run.",                 "😱"),
            new(AchievementId.Untouchable,        "Untouchable",          "Reach 500 m without a single stumble.",      "🛡"),
            new(AchievementId.FarFurtherFurthest, "Personal Best",        "Beat your previous best-distance record.",   "🚀"),
            new(AchievementId.Pyromaniac,         "Score Five Grand",     "End a run with 5,000 points or more.",       "🔥"),
            new(AchievementId.GoldenSecret,       "Hidden in Plain Code", "Discover the Golden Rabbit cheat.",          "🥕✨"),
        };

        public static IReadOnlyList<Definition> All => DefinitionTable;

        /// <summary>Raised whenever an achievement transitions from locked → unlocked.</summary>
        public static event Action<Definition> OnUnlocked;

        // --- Unlock state ---

        private const string PrefPrefix = "Ach_";

        public static bool IsUnlocked(AchievementId id)
            => PlayerPrefs.GetInt(PrefPrefix + id, 0) == 1;

        /// <summary>
        /// Mark an achievement as unlocked. No-op if already unlocked.
        /// Persists immediately and raises <see cref="OnUnlocked"/>.
        /// </summary>
        public static void Unlock(AchievementId id)
        {
            if (IsUnlocked(id)) return;
            PlayerPrefs.SetInt(PrefPrefix + id, 1);
            PlayerPrefs.Save();

            for (int i = 0; i < DefinitionTable.Length; i++)
            {
                if (DefinitionTable[i].Id == id)
                {
                    OnUnlocked?.Invoke(DefinitionTable[i]);
                    return;
                }
            }
        }

        // --- Lifetime counters ---

        public const string LifetimeCarrotsKey = "Ach_LifetimeCarrots";
        public const string LifetimeMagnetsKey = "Ach_LifetimeMagnets";

        public static int GetCounter(string key) => PlayerPrefs.GetInt(key, 0);

        /// <summary>Increment a lifetime counter and return the new value.</summary>
        public static int IncrementCounter(string key, int delta = 1)
        {
            int v = PlayerPrefs.GetInt(key, 0) + delta;
            PlayerPrefs.SetInt(key, v);
            return v; // Caller may batch PlayerPrefs.Save with other writes.
        }

        public static int UnlockedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < DefinitionTable.Length; i++)
                    if (IsUnlocked(DefinitionTable[i].Id)) n++;
                return n;
            }
        }

        public static int Total => DefinitionTable.Length;
    }
}
