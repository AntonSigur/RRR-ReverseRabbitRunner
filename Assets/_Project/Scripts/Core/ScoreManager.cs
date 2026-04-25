using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Tracks score from carrot collection and manages high score persistence.
    /// Also runs the combo / streak multiplier: collecting carrots in quick
    /// succession scales each carrot's score value (x1 → x2 → x3 → x4 → x5).
    /// A stumble or a too-long pause between collects breaks the streak.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [Header("Combo / Streak")]
        [Tooltip("Seconds allowed between consecutive carrots before the combo resets.")]
        [SerializeField] private float comboTimeWindow = 4f;

        [Tooltip("Combo counts at which the multiplier ticks up. " +
                 "Reaching index 0 = x2, index 1 = x3, etc. " +
                 "Multiplier never exceeds tierThresholds.Length + 1.")]
        [SerializeField] private int[] tierThresholds = new[] { 5, 15, 30, 60 };

        private int currentScore;
        private int highScore;
        private int runStartHighScore;
        private int carrotsCollected;
        private int maxComboReached;
        private int maxMultiplierReached = 1;
        private int maxTierReached;
        private float bestDistance;
        private float currentRunDistance;

        // Combo state
        private int comboCount;
        private int multiplier = 1;
        private float comboExpireTime;

        private const string HighScoreKey = "HighScore";
        private const string BestDistanceKey = "BestDistance";

        public int CurrentScore => currentScore;
        public int HighScore => highScore;
        public int CarrotsCollected => carrotsCollected;
        public int ComboCount => comboCount;
        public int Multiplier => multiplier;
        public int MaxComboReached => maxComboReached;
        public int MaxMultiplierReached => maxMultiplierReached;
        public int MaxTierReached => maxTierReached;
        public float BestDistance => bestDistance;
        public float CurrentRunDistance => currentRunDistance;
        public bool LastRunWasNewBestScore { get; private set; }
        public bool LastRunWasNewBestDistance { get; private set; }
        public float ComboTimeRemaining =>
            comboCount > 0 ? Mathf.Max(0f, comboExpireTime - Time.time) : 0f;
        public float ComboTimeWindow => comboTimeWindow;

        public event System.Action<int> OnScoreChanged;
        public event System.Action<int> OnHighScoreBeaten;
        /// <summary>
        /// Fires whenever the combo count or multiplier changes (including reaching 0).
        /// Args: (comboCount, multiplier, multiplierTierJustIncreased).
        /// </summary>
        public event System.Action<int, int, bool> OnComboChanged;
        /// <summary>
        /// Fires after a successful AddScore. Args: (gainedPoints, multiplierApplied, sourceWorldPos).
        /// sourceWorldPos is <see cref="Vector3.zero"/> when the gain has no spatial anchor
        /// (e.g. cheat-console adjustments) — listeners should suppress floaters in that case.
        /// </summary>
        public event System.Action<int, int, Vector3> OnScoreGained;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
            bestDistance = PlayerPrefs.GetFloat(BestDistanceKey, 0f);
            runStartHighScore = highScore;
        }

        private void Update()
        {
            // Combo expiry — uses scaled Time.time so it pauses with the game
            if (comboCount > 0 && Time.time >= comboExpireTime)
                BreakCombo();

            // Track per-run distance + max tier
            var diff = DifficultyManager.Instance;
            if (diff != null)
            {
                if (diff.CurrentDistance > currentRunDistance)
                    currentRunDistance = diff.CurrentDistance;
                if (diff.Tier > maxTierReached)
                    maxTierReached = diff.Tier;
            }
        }

        public void AddScore(int basePoints)
        {
            AddScoreInternal(basePoints, Vector3.zero, hasPos: false);
        }

        /// <summary>
        /// Awards score and emits <see cref="OnScoreGained"/> with a world-space anchor
        /// so listeners (score floaters, particles) can present the gain at the source.
        /// </summary>
        public void AddScoreAt(int basePoints, Vector3 sourceWorldPos)
        {
            AddScoreInternal(basePoints, sourceWorldPos, hasPos: true);
        }

        private void AddScoreInternal(int basePoints, Vector3 sourceWorldPos, bool hasPos)
        {
            // Bump combo first so the multiplier reflects THIS pickup
            int previousMultiplier = multiplier;
            comboCount++;
            multiplier = ComputeMultiplier(comboCount);
            comboExpireTime = Time.time + comboTimeWindow;

            if (comboCount > maxComboReached) maxComboReached = comboCount;
            if (multiplier > maxMultiplierReached) maxMultiplierReached = multiplier;

            int gained = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, basePoints) * multiplier * EasterEggs.ScoreBonusMultiplier));
            currentScore += gained;
            carrotsCollected++;
            OnScoreChanged?.Invoke(currentScore);
            OnComboChanged?.Invoke(comboCount, multiplier, multiplier > previousMultiplier);
            OnScoreGained?.Invoke(gained, multiplier, hasPos ? sourceWorldPos : Vector3.zero);

            if (currentScore > highScore)
            {
                highScore = currentScore;
                PlayerPrefs.SetInt(HighScoreKey, highScore);
                PlayerPrefs.Save();
                OnHighScoreBeaten?.Invoke(highScore);
            }
        }

        /// <summary>
        /// Reset the combo streak (e.g. on stumble or expiry).
        /// </summary>
        public void BreakCombo()
        {
            if (comboCount == 0 && multiplier == 1) return;
            comboCount = 0;
            multiplier = 1;
            OnComboChanged?.Invoke(0, 1, false);
        }

        public void ResetScore()
        {
            currentScore = 0;
            carrotsCollected = 0;
            currentRunDistance = 0f;
            maxComboReached = 0;
            maxMultiplierReached = 1;
            maxTierReached = 0;
            runStartHighScore = highScore;
            LastRunWasNewBestScore = false;
            LastRunWasNewBestDistance = false;
            BreakCombo();
            OnScoreChanged?.Invoke(currentScore);
        }

        /// <summary>
        /// Called by GameManager when a run ends. Persists best distance and
        /// snapshots whether either record was beaten this run, so the
        /// game-over UI can show "NEW BEST!" badges.
        /// </summary>
        public void CommitRunResults()
        {
            // High score is already saved live in AddScore — but we set the flag
            // here too so the game-over screen can light up the badge.
            LastRunWasNewBestScore = currentScore > runStartHighScore;

            float distNow = currentRunDistance;
            if (distNow > bestDistance)
            {
                bestDistance = distNow;
                PlayerPrefs.SetFloat(BestDistanceKey, bestDistance);
                PlayerPrefs.Save();
                LastRunWasNewBestDistance = true;
            }
            else
            {
                LastRunWasNewBestDistance = false;
            }
            OnRunCommitted?.Invoke();
        }

        public event System.Action OnRunCommitted;

        private int ComputeMultiplier(int combo)
        {
            int tier = 1;
            if (tierThresholds == null) return tier;
            for (int i = 0; i < tierThresholds.Length; i++)
            {
                if (combo >= tierThresholds[i]) tier = i + 2;
            }
            return tier;
        }
    }
}
