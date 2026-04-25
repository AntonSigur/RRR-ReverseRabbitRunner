using UnityEngine;

namespace ReverseRabbitRunner.UI
{
    /// <summary>
    /// Center-screen banner that flashes when a run milestone fires. Uses
    /// unscaled time so it animates correctly even if Time.timeScale dips.
    /// Subscribes to <see cref="Core.RunMilestones.OnMilestone"/>.
    /// </summary>
    public class MilestoneBanner : MonoBehaviour
    {
        public static MilestoneBanner Instance { get; private set; }

        private const float Duration = 2.0f;
        private const float FadeIn = 0.15f;
        private const float FadeOut = 0.5f;

        private string currentLabel;
        private float startedAt;
        private bool active;

        private GUIStyle mainStyle;
        private GUIStyle subStyle;
        private bool stylesReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (Instance != null) return;
            var go = new GameObject("[MilestoneBanner]");
            DontDestroyOnLoad(go);
            go.AddComponent<MilestoneBanner>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            Core.RunMilestones.OnMilestone += HandleMilestone;
        }

        private void OnDisable()
        {
            Core.RunMilestones.OnMilestone -= HandleMilestone;
        }

        private void HandleMilestone(int meters, string label)
        {
            currentLabel = label;
            startedAt = Time.unscaledTime;
            active = true;

            // Reuse the "collect special" chime; cheaper than adding a new clip
            // and it already fits the arcade feel.
            Core.AudioManager.Instance?.PlayCollectSpecial(meters >= 5000 ? 2 : 1);
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            mainStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 72,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            stylesReady = true;
        }

        private void OnGUI()
        {
            if (!active) return;
            float t = Time.unscaledTime - startedAt;
            if (t >= Duration) { active = false; return; }

            EnsureStyles();

            // Alpha envelope: fade-in, hold, fade-out.
            float a;
            if (t < FadeIn) a = t / FadeIn;
            else if (t > Duration - FadeOut) a = Mathf.Clamp01((Duration - t) / FadeOut);
            else a = 1f;

            // Scale overshoot pulse at the start for a snappy pop.
            float scale = t < 0.25f
                ? Mathf.Lerp(1.4f, 1f, Mathf.SmoothStep(0f, 1f, t / 0.25f))
                : 1f;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.32f;
            float w = 640f;
            float h = 120f;

            var matrix = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), new Vector2(cx, cy));

            // Shadow halo behind the text for readability over any world colour.
            GUI.color = new Color(0f, 0f, 0f, 0.45f * a);
            GUI.DrawTexture(new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h), Texture2D.whiteTexture);

            // Gold/amber gradient feel via two-pass draw (shadow + fill).
            var shadow = new Color(0.1f, 0.06f, 0f, a);
            var fill = new Color(1f, 0.86f, 0.28f, a);

            mainStyle.normal.textColor = shadow;
            GUI.Label(new Rect(cx - w * 0.5f + 3, cy - h * 0.5f + 3, w, h * 0.7f), currentLabel, mainStyle);
            mainStyle.normal.textColor = fill;
            GUI.Label(new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h * 0.7f), currentLabel, mainStyle);

            subStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f * a);
            GUI.Label(new Rect(cx - w * 0.5f, cy + h * 0.15f, w, 30f), "MILESTONE REACHED", subStyle);

            GUI.matrix = matrix;
            GUI.color = Color.white;
        }
    }
}
