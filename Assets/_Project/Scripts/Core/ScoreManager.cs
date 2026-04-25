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
        private int carrotsCollected;

        // Combo state
        private int comboCount;
        private int multiplier = 1;
        private float comboExpireTime;

        private const string HighScoreKey = "HighScore";

        public int CurrentScore => currentScore;
        public int HighScore => highScore;
        public int CarrotsCollected => carrotsCollected;
        public int ComboCount => comboCount;
        public int Multiplier => multiplier;
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
        }

        private void Update()
        {
            // Combo expiry — uses scaled Time.time so it pauses with the game
            if (comboCount > 0 && Time.time >= comboExpireTime)
                BreakCombo();
        }

        public void AddScore(int basePoints)
        {
            // Bump combo first so the multiplier reflects THIS pickup
            int previousMultiplier = multiplier;
            comboCount++;
            multiplier = ComputeMultiplier(comboCount);
            comboExpireTime = Time.time + comboTimeWindow;

            int gained = Mathf.Max(1, basePoints) * multiplier;
            currentScore += gained;
            carrotsCollected++;
            OnScoreChanged?.Invoke(currentScore);
            OnComboChanged?.Invoke(comboCount, multiplier, multiplier > previousMultiplier);

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
            BreakCombo();
            OnScoreChanged?.Invoke(currentScore);
        }

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
