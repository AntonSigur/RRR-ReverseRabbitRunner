using System;
using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Centralised registry for hidden cheats / easter eggs. PlayerPrefs-backed.
    /// Currently implements the "Golden Rabbit" cheat unlocked by the Konami
    /// code on the title screen.
    ///
    /// Design notes:
    ///   * All state lives in PlayerPrefs so it survives sessions.
    ///   * State is exposed through static properties; setters persist
    ///     immediately because toggles are rare button-style events.
    ///   * Visual / score systems read state lazily — they don't need to be
    ///     wired together. ScoreManager multiplies by ScoreBonusMultiplier;
    ///     RabbitController tints body materials when GoldenRabbitUnlocked.
    /// </summary>
    public static class EasterEggs
    {
        private const string GoldenRabbitKey = "EE_GoldenRabbit";

        /// <summary>Score multiplier applied while the cheat is on.</summary>
        public const float GoldenRabbitScoreBonus = 1.5f;

        /// <summary>Tint applied to the rabbit body when the cheat is on.</summary>
        public static readonly Color GoldenRabbitTint = new Color(1f, 0.82f, 0.18f);

        /// <summary>Raised whenever the Golden Rabbit flag changes value.</summary>
        public static event Action<bool> OnGoldenRabbitChanged;

        public static bool GoldenRabbitUnlocked
        {
            get => PlayerPrefs.GetInt(GoldenRabbitKey, 0) == 1;
            set
            {
                bool current = GoldenRabbitUnlocked;
                if (current == value) return;
                PlayerPrefs.SetInt(GoldenRabbitKey, value ? 1 : 0);
                PlayerPrefs.Save();
                OnGoldenRabbitChanged?.Invoke(value);
            }
        }

        /// <summary>Score multiplier — 1f when no cheats active.</summary>
        public static float ScoreBonusMultiplier => GoldenRabbitUnlocked ? GoldenRabbitScoreBonus : 1f;
    }
}
