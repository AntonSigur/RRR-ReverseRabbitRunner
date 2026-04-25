using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Centralised escalation curve. Reads the player's run distance from
    /// ChunkManager and exposes a discrete <see cref="Tier"/> plus continuous
    /// modifiers consumed by the rabbit, farmer and HUD.
    ///
    /// One source of truth for "the game gets harder over time", so the
    /// individual subsystems don't each invent their own curve.
    /// </summary>
    public class DifficultyManager : MonoBehaviour
    {
        public static DifficultyManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("[DifficultyManager]");
                go.AddComponent<DifficultyManager>();
                DontDestroyOnLoad(go);
            }
        }

        [Header("Tier thresholds (metres)")]
        [Tooltip("Distance at which each successive tier begins. Stay sorted ascending.")]
        [SerializeField]
        private float[] tierThresholds = { 0f, 250f, 600f, 1000f, 1500f, 2200f, 3000f, 4000f, 5500f };

        [Header("Speed ramp")]
        [Tooltip("Target forward speed (units/sec) at each tier. Length must match tierThresholds.")]
        [SerializeField]
        private float[] tierSpeedTargets = { 10f, 12f, 14f, 16f, 18f, 21f, 24f, 27f, 30f };

        [Tooltip("How fast baseSpeed lerps toward the tier target (units/sec/sec).")]
        [SerializeField] private float speedRampPerSecond = 0.6f;

        [Header("Farmer aggression")]
        [Tooltip("Multiplier on the farmer's resting baseDistance per tier. 1.0 = original, lower = closer.")]
        [SerializeField]
        private float[] tierFarmerCloseness = { 1.00f, 0.95f, 0.85f, 0.75f, 0.65f, 0.58f, 0.52f, 0.48f, 0.42f };

        // Cached read of distance to avoid hammering FindAnyObjectByType every frame
        private World.ChunkManager chunkMgr;
        private int cachedTier;
        private float cachedDistance;

        public int Tier => cachedTier;
        public int MaxTier => tierThresholds.Length - 1;
        public float CurrentDistance => cachedDistance;
        public float SpeedTarget => tierSpeedTargets[cachedTier];
        public float SpeedRampPerSecond => speedRampPerSecond;
        public float FarmerClosenessMultiplier => tierFarmerCloseness[cachedTier];

        /// <summary>Distance into the current tier (0..1) for HUD progress visuals.</summary>
        public float TierProgress
        {
            get
            {
                if (cachedTier >= MaxTier) return 1f;
                float lo = tierThresholds[cachedTier];
                float hi = tierThresholds[cachedTier + 1];
                if (hi <= lo) return 1f;
                return Mathf.Clamp01((cachedDistance - lo) / (hi - lo));
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Defensive: if arrays got out of sync, clamp to the shorter length.
            int len = Mathf.Min(tierThresholds.Length,
                Mathf.Min(tierSpeedTargets.Length, tierFarmerCloseness.Length));
            if (len < tierThresholds.Length)
            {
                System.Array.Resize(ref tierThresholds, len);
                System.Array.Resize(ref tierSpeedTargets, len);
                System.Array.Resize(ref tierFarmerCloseness, len);
                Debug.LogWarning($"[DifficultyManager] Tier arrays were uneven; clamped to {len}.");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (chunkMgr == null)
                chunkMgr = FindAnyObjectByType<World.ChunkManager>();
            if (chunkMgr == null) return;

            cachedDistance = chunkMgr.TotalDistance;

            // Walk thresholds (small array — linear scan is fine and avoids dependency
            // on monotonic distance, which origin-shift could violate momentarily).
            int t = 0;
            for (int i = tierThresholds.Length - 1; i >= 0; i--)
            {
                if (cachedDistance >= tierThresholds[i]) { t = i; break; }
            }

            if (t != cachedTier)
            {
                int prev = cachedTier;
                cachedTier = t;
                if (t > prev) OnTierUp?.Invoke(t);
            }
        }

        public event System.Action<int> OnTierUp;
    }
}
