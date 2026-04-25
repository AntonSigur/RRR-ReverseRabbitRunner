using System;
using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// First-run soft-start tutorial: clamps difficulty to tier 0 for the
    /// opening seconds and surfaces context-sensitive prompts (lane swap,
    /// jump, collect a carrot). Once the player completes all three steps —
    /// or 30 s elapse — the tutorial ends and difficulty resumes its curve.
    ///
    /// State machine is shared via static fields so multiple subsystems can
    /// observe it without coupling. Persistence is intentionally minimal:
    /// just one PlayerPrefs flag, written when the first run ends so any
    /// crash or quit *during* the first run still re-shows the tutorial.
    /// </summary>
    public static class Tutorial
    {
        public enum Step
        {
            LaneSwitch,
            Jump,
            Carrots,
            Done,
        }

        public const float MaxDuration = 30f;
        private const string FirstRunKey = "HasPlayedBefore";

        public static bool IsFirstRun => PlayerPrefs.GetInt(FirstRunKey, 0) == 0;

        public static bool IsActive { get; private set; }
        public static Step Current { get; private set; } = Step.Done;
        public static float StartedAt { get; private set; }

        public static event Action<Step> OnStepChanged;

        /// <summary>Activate at run start when this is a first run.</summary>
        public static void Begin()
        {
            IsActive = true;
            Current = Step.LaneSwitch;
            StartedAt = Time.time;
            OnStepChanged?.Invoke(Current);
        }

        /// <summary>Force-end the tutorial (e.g. timeout, run end, opt-out).</summary>
        public static void End()
        {
            if (!IsActive && Current == Step.Done) return;
            IsActive = false;
            Current = Step.Done;
            OnStepChanged?.Invoke(Current);

            // Mark the player as no longer a first-timer.
            if (IsFirstRun)
            {
                PlayerPrefs.SetInt(FirstRunKey, 1);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Advance the state machine when an event fires.</summary>
        public static void Advance(Step completed)
        {
            if (!IsActive) return;
            if ((int)completed != (int)Current) return;
            Step next = (Step)((int)Current + 1);
            Current = next;
            OnStepChanged?.Invoke(Current);
            if (Current == Step.Done) End();
        }

        /// <summary>True while difficulty should remain at base tier.</summary>
        public static bool HoldDifficulty => IsActive;

        /// <summary>The text prompt for the current step (empty when none).</summary>
        public static string CurrentPrompt => Current switch
        {
            Step.LaneSwitch => "← / → or A / D — swipe to switch lanes",
            Step.Jump       => "Space / W / ↑ — swipe up or tap to jump",
            Step.Carrots    => "Collect 🥕 carrots — chain them for combos!",
            _ => string.Empty,
        };
    }
}
