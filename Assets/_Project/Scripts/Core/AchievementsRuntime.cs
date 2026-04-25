using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Drives the achievement system at runtime: subscribes to gameplay events
    /// (<see cref="ScoreManager"/>, <see cref="Player.RabbitController"/>,
    /// <see cref="PowerUps.MagnetEffect"/>, <see cref="EasterEggs"/>) and calls
    /// <see cref="Achievements.Unlock"/> when thresholds are crossed. Updates
    /// lifetime counters in <see cref="PlayerPrefs"/>.
    ///
    /// Auto-spawned via <see cref="RuntimeInitializeOnLoadMethodAttribute"/>
    /// as a DontDestroyOnLoad singleton — works in every scene.
    ///
    /// Subscriptions retry lazily in Update because game objects (rabbit,
    /// score manager) may spawn after the first frame on a fresh scene load.
    /// </summary>
    public class AchievementsRuntime : MonoBehaviour
    {
        public static AchievementsRuntime Instance { get; private set; }

        // Per-run counters (reset on run start).
        private int runNearMissCount;
        private bool runStumbled;
        private float runStartDistance;

        // Wiring state.
        private Player.RabbitController cachedRabbit;
        private ScoreManager cachedScore;
        private bool subscribedRabbit;
        private bool subscribedScore;
        private bool subscribedMagnet;
        private bool subscribedGolden;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[AchievementsRuntime]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AchievementsRuntime>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Static event subscriptions (don't depend on scene objects).
            PowerUps.MagnetEffect.OnMagnetActivated += OnMagnetActivated;
            subscribedMagnet = true;

            EasterEggs.OnGoldenRabbitChanged += OnGoldenRabbitChanged;
            subscribedGolden = true;

            // Retroactive: if user already unlocked the cheat in a previous
            // build (before this system existed), grant the matching badge.
            if (EasterEggs.GoldenRabbitUnlocked)
                Achievements.Unlock(AchievementId.GoldenSecret);
        }

        private void Update()
        {
            if (!subscribedRabbit)
            {
                var rabbit = FindAnyObjectByType<Player.RabbitController>();
                if (rabbit != null)
                {
                    cachedRabbit = rabbit;
                    rabbit.OnNearMiss += OnNearMiss;
                    rabbit.OnStumble  += OnStumble;
                    rabbit.OnCollectCarrot += OnCollectCarrot;
                    subscribedRabbit = true;
                    BeginRun();
                }
            }

            if (!subscribedScore)
            {
                var sm = ScoreManager.Instance;
                if (sm != null)
                {
                    cachedScore = sm;
                    sm.OnComboChanged += OnComboChanged;
                    sm.OnRunCommitted += OnRunCommitted;
                    subscribedScore = true;
                }
            }

            // Distance-based achievements check while a run is active.
            if (cachedScore != null && cachedRabbit != null)
            {
                float dist = cachedScore.CurrentRunDistance;
                if (dist >= 100f)  Achievements.Unlock(AchievementId.FirstHundred);
                if (dist >= 1000f) Achievements.Unlock(AchievementId.FirstKilometer);
                if (dist >= 5000f) Achievements.Unlock(AchievementId.MarathonRunner);

                // Untouchable — 500m without a stumble. We treat distance from
                // the last reset as the qualifying span; runStumbled clears at
                // run start.
                if (!runStumbled && dist - runStartDistance >= 500f)
                    Achievements.Unlock(AchievementId.Untouchable);

                if (cachedScore.MaxTierReached >= 8)
                    Achievements.Unlock(AchievementId.TierClimber);
            }

            // Detect rabbit/scene swap (re-grab on next frame).
            if (cachedRabbit == null && subscribedRabbit) subscribedRabbit = false;
            if (cachedScore == null && subscribedScore) subscribedScore = false;
        }

        private void BeginRun()
        {
            runNearMissCount = 0;
            runStumbled = false;
            runStartDistance = cachedScore != null ? cachedScore.CurrentRunDistance : 0f;
        }

        // --- Event handlers ---

        private void OnNearMiss(GameObject _)
        {
            runNearMissCount++;
            if (runNearMissCount >= 10)
                Achievements.Unlock(AchievementId.NearMissPro);
        }

        private void OnStumble(float _)
        {
            runStumbled = true;
        }

        private void OnCollectCarrot(GameObject _)
        {
            int total = Achievements.IncrementCounter(Achievements.LifetimeCarrotsKey);
            if (total >= 100)   Achievements.Unlock(AchievementId.TopHundredCarrots);
            if (total >= 1000)  Achievements.Unlock(AchievementId.TopThousandCarrots);
        }

        private void OnMagnetActivated()
        {
            int total = Achievements.IncrementCounter(Achievements.LifetimeMagnetsKey);
            if (total >= 25) Achievements.Unlock(AchievementId.MagnetMagnet);
            // Counter writes are batched; flush opportunistically.
            PlayerPrefs.Save();
        }

        private void OnComboChanged(int comboCount, int multiplier, bool tierUp)
        {
            if (multiplier >= 5)  Achievements.Unlock(AchievementId.ComboNovice);
            if (multiplier >= 10) Achievements.Unlock(AchievementId.ComboMaster);
            if (multiplier >= 25) Achievements.Unlock(AchievementId.ComboLegend);
        }

        private void OnRunCommitted()
        {
            if (cachedScore == null) return;
            if (cachedScore.LastRunWasNewBestDistance)
                Achievements.Unlock(AchievementId.FarFurtherFurthest);
            if (cachedScore.CurrentScore >= 5000)
                Achievements.Unlock(AchievementId.Pyromaniac);

            // Persist lifetime counters once per run end.
            PlayerPrefs.Save();

            // Prepare for the next run.
            BeginRun();
        }

        private void OnGoldenRabbitChanged(bool unlocked)
        {
            if (unlocked) Achievements.Unlock(AchievementId.GoldenSecret);
        }

        private void OnDestroy()
        {
            if (subscribedMagnet)
                PowerUps.MagnetEffect.OnMagnetActivated -= OnMagnetActivated;
            if (subscribedGolden)
                EasterEggs.OnGoldenRabbitChanged -= OnGoldenRabbitChanged;
            if (subscribedRabbit && cachedRabbit != null)
            {
                cachedRabbit.OnNearMiss -= OnNearMiss;
                cachedRabbit.OnStumble  -= OnStumble;
                cachedRabbit.OnCollectCarrot -= OnCollectCarrot;
            }
            if (subscribedScore && cachedScore != null)
            {
                cachedScore.OnComboChanged -= OnComboChanged;
                cachedScore.OnRunCommitted -= OnRunCommitted;
            }
            if (Instance == this) Instance = null;
        }
    }
}
