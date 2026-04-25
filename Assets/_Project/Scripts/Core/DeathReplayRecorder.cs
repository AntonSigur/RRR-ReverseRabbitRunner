using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Captures the last few seconds of the rabbit and farmer positions in a
    /// fixed-size ring buffer. On Game Over, <see cref="GameHUD"/> can render
    /// a top-down replay using <see cref="GetSnapshot"/>.
    ///
    /// Records positions only (no rotations / animator state) — this keeps
    /// memory tiny and avoids any risk of perturbing scene state. The replay
    /// is rendered in 2D so it never has to re-animate the dead rabbit.
    ///
    /// Auto-spawned via <see cref="RuntimeInitializeOnLoadMethodAttribute"/>;
    /// recording starts as soon as it finds a rabbit and stops when the game
    /// transitions out of <see cref="GameManager.GameState.Playing"/>.
    /// </summary>
    public class DeathReplayRecorder : MonoBehaviour
    {
        public const float CaptureRate = 20f;       // samples / second
        public const float CaptureDuration = 3f;    // seconds retained
        public const int Capacity = 64;             // 3 s * 20 Hz, rounded up

        public struct Sample
        {
            public float TimeStamp;
            public Vector3 Rabbit;
            public Vector3 Farmer;
            public bool HasFarmer;
        }

        public static DeathReplayRecorder Instance { get; private set; }

        private readonly Sample[] buffer = new Sample[Capacity];
        private int writeIndex;
        private int sampleCount;
        private float lastSampleTime;

        private Player.RabbitController rabbit;
        private Enemies.FarmerController farmer;

        private bool recording;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[DeathReplayRecorder]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<DeathReplayRecorder>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>Number of valid samples currently in the buffer.</summary>
        public int Count => sampleCount;

        /// <summary>
        /// Returns the i-th sample in chronological order (0 == oldest,
        /// <see cref="Count"/>-1 == newest). Reading is safe at any time.
        /// </summary>
        public Sample GetSnapshot(int i)
        {
            if (sampleCount == 0) return default;
            int start = sampleCount < Capacity ? 0 : writeIndex;
            return buffer[(start + i) % Capacity];
        }

        /// <summary>Clears the buffer (called when starting a new run).</summary>
        public void ResetBuffer()
        {
            writeIndex = 0;
            sampleCount = 0;
            lastSampleTime = 0f;
        }

        private void LateUpdate()
        {
            // Re-acquire references each frame until they're available; cheap
            // because FindAnyObjectByType is only called while still null.
            if (rabbit == null) rabbit = FindAnyObjectByType<Player.RabbitController>();
            if (farmer == null) farmer = FindAnyObjectByType<Enemies.FarmerController>();

            var gm = GameManager.Instance;
            bool playing = gm != null && gm.CurrentState == GameManager.GameState.Playing;

            if (!recording && playing)
            {
                ResetBuffer();
                recording = true;
            }
            else if (recording && !playing)
            {
                // Game just ended — freeze the buffer.
                recording = false;
            }

            if (!recording || rabbit == null) return;

            float now = Time.time;
            if (now - lastSampleTime < 1f / CaptureRate) return;
            lastSampleTime = now;

            var s = new Sample
            {
                TimeStamp = now,
                Rabbit = rabbit.transform.position,
                HasFarmer = farmer != null,
                Farmer = farmer != null ? farmer.transform.position : Vector3.zero,
            };
            buffer[writeIndex] = s;
            writeIndex = (writeIndex + 1) % Capacity;
            if (sampleCount < Capacity) sampleCount++;
        }
    }
}
