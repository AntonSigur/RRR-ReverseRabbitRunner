using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Watches the live run distance against the player's saved best-distance
    /// record and triggers a one-shot celebration banner the instant the
    /// current run surpasses it. Fires only while the game is in the Playing
    /// state, only once per run, and only when there is an existing record
    /// to beat (i.e. <see cref="ScoreManager.BestDistance"/> &gt; 0).
    ///
    /// Pairs with the existing <see cref="UI.MilestoneBanner"/> by calling
    /// its public <c>Trigger</c> hook — no new UI plumbing required.
    /// </summary>
    public class PersonalBestBeacon : MonoBehaviour
    {
        public static PersonalBestBeacon Instance { get; private set; }

        private float baselineBest;
        private bool firedThisRun;
        private bool armed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (Instance != null) return;
            var go = new GameObject("[PersonalBestBeacon]");
            DontDestroyOnLoad(go);
            go.AddComponent<PersonalBestBeacon>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            var gm = GameManager.Instance;
            if (gm != null) gm.OnGameStateChanged += OnGameStateChanged;
        }

        private void OnDisable()
        {
            var gm = GameManager.Instance;
            if (gm != null) gm.OnGameStateChanged -= OnGameStateChanged;
        }

        private void Update()
        {
            // Late-bind to GameManager: it's created at scene load and our
            // OnEnable fires before that in some scenes.
            if (!armed)
            {
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    gm.OnGameStateChanged += OnGameStateChanged;
                    armed = true;
                    OnGameStateChanged(gm.CurrentState);
                }
            }

            if (firedThisRun) return;
            var gmNow = GameManager.Instance;
            if (gmNow == null || gmNow.CurrentState != GameManager.GameState.Playing) return;
            var sm = ScoreManager.Instance;
            if (sm == null) return;

            // Only meaningful when there is a previous record to beat.
            if (baselineBest <= 0.5f) return;

            if (sm.CurrentRunDistance > baselineBest)
            {
                firedThisRun = true;
                int rounded = Mathf.RoundToInt(baselineBest);
                UI.MilestoneBanner.Instance?.Trigger(
                    "PERSONAL BEST!",
                    $"Surpassed {rounded} m — keep going!",
                    chimeIndex: 2);
            }
        }

        private void OnGameStateChanged(GameManager.GameState state)
        {
            if (state == GameManager.GameState.Playing)
            {
                // Capture the bar to beat at the moment this run starts.
                // Subsequent re-entries to Playing (e.g. resume from pause)
                // must NOT reset, so we key on whether we've fired yet.
                var sm = ScoreManager.Instance;
                if (sm != null && sm.CurrentRunDistance < 1f)
                {
                    baselineBest = sm.BestDistance;
                    firedThisRun = false;
                }
            }
        }
    }
}
