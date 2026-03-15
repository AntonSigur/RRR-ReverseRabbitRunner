using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Shuffled music player with crossfade between tracks.
    /// Loads all clips from Resources/Music at startup and plays them in random order.
    /// Two AudioSources alternate: one fades out while the other fades in.
    /// Debug: Shift+3 = skip song, Shift+4 = toggle song name overlay.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer Instance { get; private set; }

        [Header("Playback")]
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.35f;
        [SerializeField] private float crossfadeDuration = 2.5f;
        [SerializeField] private float preFadeTime = 3.0f; // start crossfade this many seconds before track ends

        private AudioSource sourceA;
        private AudioSource sourceB;
        private AudioSource activeSource;
        private AudioSource inactiveSource;

        private List<AudioClip> playlist = new List<AudioClip>();
        private List<int> shuffleOrder = new List<int>();
        private int currentIndex;
        private bool isCrossfading;
        private string currentTrackName = "";
        private float songNameDisplayTimer;
        private bool showSongName;

        // Song name overlay style
        private GUIStyle songNameStyle;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Create two audio sources for crossfading
            sourceA = gameObject.AddComponent<AudioSource>();
            sourceA.playOnAwake = false;
            sourceA.spatialBlend = 0f;
            sourceA.loop = false;

            sourceB = gameObject.AddComponent<AudioSource>();
            sourceB.playOnAwake = false;
            sourceB.spatialBlend = 0f;
            sourceB.loop = false;

            activeSource = sourceA;
            inactiveSource = sourceB;

            LoadPlaylist();
            LoadVolumePrefs();
        }

        private void LoadPlaylist()
        {
            var clips = Resources.LoadAll<AudioClip>("Music");
            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning("[MusicPlayer] No music found in Resources/Music/");
                return;
            }

            playlist.AddRange(clips);
            Debug.Log($"[MusicPlayer] Loaded {playlist.Count} tracks");
            Shuffle();
        }

        private void Shuffle()
        {
            shuffleOrder.Clear();
            for (int i = 0; i < playlist.Count; i++)
                shuffleOrder.Add(i);

            // Fisher-Yates shuffle
            for (int i = shuffleOrder.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (shuffleOrder[i], shuffleOrder[j]) = (shuffleOrder[j], shuffleOrder[i]);
            }

            currentIndex = 0;
        }

        private void Start()
        {
            if (playlist.Count > 0)
                PlayTrack(GetNextClip());
        }

        private void Update()
        {
            // Auto-advance: start crossfade when approaching end of track
            if (activeSource.isPlaying && !isCrossfading && playlist.Count > 0)
            {
                float remaining = activeSource.clip.length - activeSource.time;
                if (remaining <= preFadeTime)
                    StartCoroutine(CrossfadeToNext());
            }

            // Handle track ending without crossfade (safety net)
            if (!activeSource.isPlaying && !isCrossfading && playlist.Count > 0)
                PlayTrack(GetNextClip());

            // Song name display timer
            if (songNameDisplayTimer > 0f)
                songNameDisplayTimer -= Time.unscaledDeltaTime;

            // Debug controls
            #if UNITY_EDITOR
            if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
            {
                // Shift+3 = skip song
                if (Keyboard.current.digit3Key.wasPressedThisFrame)
                {
                    Debug.Log("[MusicPlayer] Shift+3: Skipping song");
                    SkipTrack();
                }

                // Shift+4 = toggle song name overlay
                if (Keyboard.current.digit4Key.wasPressedThisFrame)
                {
                    showSongName = !showSongName;
                    if (showSongName)
                        songNameDisplayTimer = 999f;
                    else
                        songNameDisplayTimer = 0f;
                    Debug.Log($"[MusicPlayer] Shift+4: Song name display {(showSongName ? "ON" : "OFF")}");
                }
            }
            #endif
        }

        private AudioClip GetNextClip()
        {
            if (playlist.Count == 0) return null;

            if (currentIndex >= shuffleOrder.Count)
                Shuffle(); // Re-shuffle when all played

            var clip = playlist[shuffleOrder[currentIndex]];
            currentIndex++;
            return clip;
        }

        private void PlayTrack(AudioClip clip)
        {
            if (clip == null) return;

            activeSource.clip = clip;
            activeSource.volume = musicVolume;
            activeSource.Play();
            currentTrackName = clip.name;
            songNameDisplayTimer = 4f; // Show name for 4 seconds on new track

            Debug.Log($"[MusicPlayer] Now playing: {clip.name} ({clip.length:F1}s)");
        }

        /// <summary>
        /// Skip to next track with crossfade.
        /// </summary>
        public void SkipTrack()
        {
            if (isCrossfading) return;
            StartCoroutine(CrossfadeToNext());
        }

        private IEnumerator CrossfadeToNext()
        {
            if (isCrossfading) yield break;
            isCrossfading = true;

            var nextClip = GetNextClip();
            if (nextClip == null)
            {
                isCrossfading = false;
                yield break;
            }

            // Start the new track on the inactive source
            inactiveSource.clip = nextClip;
            inactiveSource.volume = 0f;
            inactiveSource.Play();

            currentTrackName = nextClip.name;
            songNameDisplayTimer = 4f;

            Debug.Log($"[MusicPlayer] Crossfading to: {nextClip.name}");

            // Crossfade
            float elapsed = 0f;
            float startVolume = activeSource.volume;

            while (elapsed < crossfadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / crossfadeDuration);

                // Fade out old
                activeSource.volume = Mathf.Lerp(startVolume, 0f, t);
                // Fade in new
                inactiveSource.volume = Mathf.Lerp(0f, musicVolume, t);

                yield return null;
            }

            // Stop old source
            activeSource.Stop();
            activeSource.volume = 0f;

            // Swap sources
            (activeSource, inactiveSource) = (inactiveSource, activeSource);

            isCrossfading = false;
        }

        private void OnGUI()
        {
            if (songNameDisplayTimer <= 0f) return;

            if (songNameStyle == null)
            {
                songNameStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Italic,
                    alignment = TextAnchor.LowerRight
                };
                songNameStyle.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
            }

            // Fade out the last second
            float alpha = Mathf.Clamp01(songNameDisplayTimer);
            if (!showSongName) // Only fade if not in persistent mode
            {
                var c = songNameStyle.normal.textColor;
                c.a = alpha * 0.6f;
                songNameStyle.normal.textColor = c;
            }

            GUI.Label(
                new Rect(0, Screen.height - 40, Screen.width - 15, 30),
                $"♫ {currentTrackName}",
                songNameStyle
            );
        }

        // --- Volume Control ---

        public float Volume
        {
            get => musicVolume;
            set
            {
                musicVolume = Mathf.Clamp01(value);
                if (activeSource != null && !isCrossfading)
                    activeSource.volume = musicVolume;
                PlayerPrefs.SetFloat("MusicVolume", musicVolume);
            }
        }

        private void LoadVolumePrefs()
        {
            musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.35f);
        }
    }
}
