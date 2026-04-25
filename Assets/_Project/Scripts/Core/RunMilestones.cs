using System;
using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Tracks distance-based milestones per run and fires a static event
    /// whenever one is crossed. Thresholds are inclusive and fire exactly once
    /// per run; the set resets when the game state re-enters Playing.
    /// </summary>
    public class RunMilestones : MonoBehaviour
    {
        public static RunMilestones Instance { get; private set; }

        // Ascending order. Edit here to tune the progression.
        public static readonly int[] Thresholds =
        {
            250, 500, 1000, 2500, 5000, 10000, 20000, 50000
        };

        /// <summary>Meters, human-readable label (e.g. "1 KM", "2.5 KM").</summary>
        public static event Action<int, string> OnMilestone;

        private int nextIndex;
        private GameManager.GameState lastState = GameManager.GameState.Menu;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (Instance != null) return;
            var go = new GameObject("[RunMilestones]");
            DontDestroyOnLoad(go);
            go.AddComponent<RunMilestones>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (gm.CurrentState != lastState)
            {
                if (gm.CurrentState == GameManager.GameState.Playing
                    && lastState != GameManager.GameState.Paused)
                {
                    nextIndex = 0; // fresh run
                }
                lastState = gm.CurrentState;
            }

            if (gm.CurrentState != GameManager.GameState.Playing) return;
            if (nextIndex >= Thresholds.Length) return;

            var sm = ScoreManager.Instance;
            if (sm == null) return;

            float dist = sm.CurrentRunDistance;
            while (nextIndex < Thresholds.Length && dist >= Thresholds[nextIndex])
            {
                int m = Thresholds[nextIndex++];
                OnMilestone?.Invoke(m, FormatMeters(m));
            }
        }

        private static string FormatMeters(int m)
        {
            if (m < 1000) return $"{m} M";
            float km = m / 1000f;
            return km == Mathf.Floor(km) ? $"{km:0} KM" : $"{km:0.0} KM";
        }
    }
}
