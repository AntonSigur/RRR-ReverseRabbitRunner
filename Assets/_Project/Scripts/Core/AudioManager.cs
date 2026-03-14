using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Central audio manager — plays SFX clips through a pooled AudioSource system.
    /// Singleton, survives scene loads. Clips assigned in editor or loaded from Resources.
    /// Subscribes to game events (rabbit jump, stumble, collect, death, etc.).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("SFX Clips")]
        [SerializeField] private AudioClip collectCarrot;
        [SerializeField] private AudioClip jump;
        [SerializeField] private AudioClip land;
        [SerializeField] private AudioClip stumbleSmall;
        [SerializeField] private AudioClip stumbleTall;
        [SerializeField] private AudioClip laneSwitch;
        [SerializeField] private AudioClip deathStab;
        [SerializeField] private AudioClip gameOver;
        [SerializeField] private AudioClip menuClick;
        [SerializeField] private AudioClip dangerWarning;

        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

        [Header("Audio Sources")]
        [SerializeField] private int poolSize = 4;

        private AudioSource[] sourcePool;
        private int nextSource;
        private AudioSource dangerSource; // looping danger sound

        // Event wiring state
        private Player.RabbitController cachedRabbit;
        private bool wasGrounded = true;
        private bool subscribedToRabbit;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Create audio source pool
            sourcePool = new AudioSource[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f; // 2D
                sourcePool[i] = src;
            }

            // Dedicated looping source for danger warning
            dangerSource = gameObject.AddComponent<AudioSource>();
            dangerSource.playOnAwake = false;
            dangerSource.spatialBlend = 0f;
            dangerSource.loop = true;
            dangerSource.volume = 0f;

            // Load clips from Resources if not assigned in editor
            LoadClipsFromResources();
            dangerSource.clip = dangerWarning;

            LoadVolumePrefs();
        }

        private void LoadClipsFromResources()
        {
            if (collectCarrot == null) collectCarrot = Resources.Load<AudioClip>("SFX/sfx_collect_carrot");
            if (jump == null) jump = Resources.Load<AudioClip>("SFX/sfx_jump");
            if (land == null) land = Resources.Load<AudioClip>("SFX/sfx_land");
            if (stumbleSmall == null) stumbleSmall = Resources.Load<AudioClip>("SFX/sfx_stumble_small");
            if (stumbleTall == null) stumbleTall = Resources.Load<AudioClip>("SFX/sfx_stumble_tall");
            if (laneSwitch == null) laneSwitch = Resources.Load<AudioClip>("SFX/sfx_lane_switch");
            if (deathStab == null) deathStab = Resources.Load<AudioClip>("SFX/sfx_death_stab");
            if (gameOver == null) gameOver = Resources.Load<AudioClip>("SFX/sfx_game_over");
            if (menuClick == null) menuClick = Resources.Load<AudioClip>("SFX/sfx_menu_click");
            if (dangerWarning == null) dangerWarning = Resources.Load<AudioClip>("SFX/sfx_danger");
        }

        private void Start()
        {
            TrySubscribeToRabbit();
        }

        private void Update()
        {
            if (!subscribedToRabbit)
                TrySubscribeToRabbit();

            // Detect landing (was airborne, now grounded)
            if (cachedRabbit != null && cachedRabbit.IsAlive)
            {
                bool grounded = cachedRabbit.IsGrounded;
                if (grounded && !wasGrounded)
                    PlaySFX(land, 0.6f);
                wasGrounded = grounded;
            }

            // Update danger warning volume based on farmer proximity
            UpdateDangerWarning();
        }

        private void TrySubscribeToRabbit()
        {
            if (subscribedToRabbit) return;

            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null) return;

            cachedRabbit = playerObj.GetComponent<Player.RabbitController>();
            if (cachedRabbit == null) return;

            cachedRabbit.OnCollectCarrot += OnCollectCarrot;
            cachedRabbit.OnStumble += OnStumble;
            subscribedToRabbit = true;
        }

        private void OnDestroy()
        {
            if (cachedRabbit != null)
            {
                cachedRabbit.OnCollectCarrot -= OnCollectCarrot;
                cachedRabbit.OnStumble -= OnStumble;
            }
        }

        // --- Event Handlers ---

        private void OnCollectCarrot(GameObject carrot)
        {
            // Carrot collect uses a short burst (only first ~0.5s of the clip)
            PlaySFXTrimmed(collectCarrot, 0.8f, 0.5f);
        }

        private void OnStumble(float penalty)
        {
            // Use tall stumble sound for big penalties, small for minor
            var clip = penalty >= 3f ? stumbleTall : stumbleSmall;
            PlaySFX(clip, 1f);
        }

        // --- Public Play Methods (called from other scripts) ---

        public void PlayJump()
        {
            PlaySFX(jump, 0.7f);
        }

        public void PlayLaneSwitch()
        {
            PlaySFX(laneSwitch, 0.5f);
        }

        public void PlayDeathStab()
        {
            PlaySFX(deathStab, 1f);
        }

        public void PlayGameOver()
        {
            PlaySFX(gameOver, 1f);
        }

        public void PlayMenuClick()
        {
            PlaySFX(menuClick, 0.7f);
        }

        /// <summary>
        /// Play an arbitrary clip at the given volume scale.
        /// </summary>
        public void PlaySFX(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;

            var src = sourcePool[nextSource];
            nextSource = (nextSource + 1) % sourcePool.Length;

            src.clip = clip;
            src.volume = sfxVolume * masterVolume * volumeScale;
            src.pitch = 1f + Random.Range(-0.05f, 0.05f); // slight variation
            src.Play();
        }

        /// <summary>
        /// Play only the first `maxSeconds` of a clip (stops playback after that).
        /// Useful for making long AI-generated clips sound short and snappy.
        /// </summary>
        public void PlaySFXTrimmed(AudioClip clip, float volumeScale, float maxSeconds)
        {
            if (clip == null) return;

            var src = sourcePool[nextSource];
            nextSource = (nextSource + 1) % sourcePool.Length;

            src.clip = clip;
            src.volume = sfxVolume * masterVolume * volumeScale;
            src.pitch = 1f + Random.Range(-0.05f, 0.05f);
            src.Play();
            StartCoroutine(StopAfter(src, maxSeconds));
        }

        private System.Collections.IEnumerator StopAfter(AudioSource src, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (src != null && src.isPlaying)
            {
                // Quick fade-out over 0.05s to avoid click
                float startVol = src.volume;
                float elapsed = 0f;
                while (elapsed < 0.05f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    src.volume = Mathf.Lerp(startVol, 0f, elapsed / 0.05f);
                    yield return null;
                }
                src.Stop();
                src.volume = startVol;
            }
        }

        // --- Danger Warning (proximity-based looping) ---

        private void UpdateDangerWarning()
        {
            if (dangerWarning == null || dangerSource == null) return;
            if (cachedRabbit == null || !cachedRabbit.IsAlive)
            {
                dangerSource.volume = 0f;
                return;
            }

            var farmerObj = GameObject.FindGameObjectWithTag("Farmer");
            if (farmerObj == null) return;

            float dist = Vector3.Distance(farmerObj.transform.position, cachedRabbit.transform.position);

            // Fade in danger sound: starts at 4 units, full at 2 units
            // (farmer's baseDistance is 5, so no sound during normal play)
            float t = Mathf.InverseLerp(4f, 2f, dist);
            float targetVol = t * sfxVolume * masterVolume * 0.4f;
            dangerSource.volume = Mathf.Lerp(dangerSource.volume, targetVol, Time.deltaTime * 3f);

            if (t > 0.01f && !dangerSource.isPlaying)
                dangerSource.Play();
            else if (t <= 0.01f && dangerSource.isPlaying)
                dangerSource.Stop();
        }

        // --- Volume Control ---

        public float SFXVolume
        {
            get => sfxVolume;
            set
            {
                sfxVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
            }
        }

        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                AudioListener.volume = masterVolume;
                PlayerPrefs.SetFloat("MasterVolume", masterVolume);
            }
        }

        /// <summary>
        /// Load saved volume preferences.
        /// </summary>
        public void LoadVolumePrefs()
        {
            sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
            masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
            AudioListener.volume = masterVolume;
        }
    }
}
