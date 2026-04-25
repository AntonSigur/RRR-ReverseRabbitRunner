using UnityEngine;

namespace ReverseRabbitRunner.UI
{
    /// <summary>
    /// World-anchored "+N" score popups that rise and fade above the source of a
    /// score gain. Listens to <see cref="Core.ScoreManager.OnScoreGained"/> for
    /// carrots / near-misses and renders via OnGUI by projecting the captured
    /// world position to screen space each frame.
    ///
    /// Pooled to a fixed ring buffer (no per-pop allocations). Suppresses itself
    /// when the gain has no spatial anchor (Vector3.zero) — that path is reserved
    /// for cheat / debug score adjustments.
    /// </summary>
    public class ScoreFloaterManager : MonoBehaviour
    {
        public static ScoreFloaterManager Instance { get; private set; }

        private const int Capacity = 16;
        private const float Lifetime = 0.9f;
        private const float RiseDistance = 70f;   // pixels travelled while alive

        private struct Floater
        {
            public Vector3 worldPos;
            public string text;
            public Color color;
            public float spawnTimeUnscaled;
            public bool active;
        }

        private readonly Floater[] floaters = new Floater[Capacity];
        private int nextSlot;

        private GUIStyle style;
        private GUIStyle shadowStyle;
        private Camera cam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Re-bootstrap on every scene load: in the menu we still want the
            // singleton ready in case ScoreManager is DontDestroyOnLoad and a
            // gameplay scene was previously visited.
            if (Instance != null) return;
            var go = new GameObject("ScoreFloaterManager");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ScoreFloaterManager>();
        }

        private void OnEnable()
        {
            HookScoreManager();
        }

        private void Update()
        {
            // ScoreManager is created lazily — keep retrying until subscribed.
            if (Core.ScoreManager.Instance != null && !subscribed)
                HookScoreManager();
        }

        private bool subscribed;

        private void HookScoreManager()
        {
            var sm = Core.ScoreManager.Instance;
            if (sm == null || subscribed) return;
            sm.OnScoreGained += OnScoreGained;
            subscribed = true;
        }

        private void OnDisable()
        {
            var sm = Core.ScoreManager.Instance;
            if (sm != null && subscribed)
            {
                sm.OnScoreGained -= OnScoreGained;
            }
            subscribed = false;
        }

        private void OnScoreGained(int gained, int multiplier, Vector3 sourceWorldPos)
        {
            // Suppress floater for unanchored gains (cheat console, etc).
            if (sourceWorldPos == Vector3.zero) return;
            if (gained <= 0) return;

            int slot = nextSlot;
            nextSlot = (nextSlot + 1) % Capacity;

            floaters[slot].worldPos = sourceWorldPos;
            floaters[slot].text = multiplier > 1
                ? $"+{gained} x{multiplier}"
                : $"+{gained}";
            floaters[slot].color = ColorForMultiplier(multiplier);
            floaters[slot].spawnTimeUnscaled = Time.unscaledTime;
            floaters[slot].active = true;
        }

        private static Color ColorForMultiplier(int m)
        {
            // White → yellow → orange → red as multiplier climbs.
            if (m >= 5) return new Color(1f, 0.35f, 0.25f);
            if (m >= 4) return new Color(1f, 0.55f, 0.20f);
            if (m >= 3) return new Color(1f, 0.78f, 0.20f);
            if (m >= 2) return new Color(1f, 0.92f, 0.40f);
            return Color.white;
        }

        private void EnsureStyles()
        {
            if (style != null) return;
            style = new GUIStyle
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            shadowStyle = new GUIStyle(style);
        }

        private Camera GetCam()
        {
            if (cam != null && cam.isActiveAndEnabled) return cam;
            cam = Camera.main;
            return cam;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;

            var camera = GetCam();
            if (camera == null) return;
            EnsureStyles();

            float now = Time.unscaledTime;
            for (int i = 0; i < Capacity; i++)
            {
                if (!floaters[i].active) continue;
                float age = now - floaters[i].spawnTimeUnscaled;
                if (age >= Lifetime)
                {
                    floaters[i].active = false;
                    continue;
                }
                float t = age / Lifetime;            // 0 → 1
                float alpha = 1f - t * t;             // ease-out fade
                float rise = RiseDistance * (1f - Mathf.Pow(1f - t, 2f)); // ease-out rise

                Vector3 screen = camera.WorldToScreenPoint(floaters[i].worldPos);
                if (screen.z <= 0f) continue; // behind camera

                // Unity GUI Y is inverted vs. screen Y.
                float guiX = screen.x;
                float guiY = Screen.height - screen.y - rise;

                // Pop-in scale at birth, settle to 1.
                float scale = 1f + Mathf.Max(0f, 0.4f - t) * 1.4f;
                int fontSize = Mathf.RoundToInt(24f * scale);
                style.fontSize = fontSize;
                shadowStyle.fontSize = fontSize;

                var rect = new Rect(guiX - 80f, guiY - 18f, 160f, 36f);
                var prevColor = GUI.color;

                // Two-pass: dark shadow then color label, for readability over busy
                // ground textures.
                GUI.color = new Color(0f, 0f, 0f, alpha * 0.65f);
                var shadowRect = rect;
                shadowRect.x += 2f; shadowRect.y += 2f;
                GUI.Label(shadowRect, floaters[i].text, shadowStyle);

                var c = floaters[i].color;
                c.a = alpha;
                GUI.color = c;
                GUI.Label(rect, floaters[i].text, style);

                GUI.color = prevColor;
            }
        }

        /// <summary>Manual reset — used by Game Over to clear lingering pops.</summary>
        public void ClearAll()
        {
            for (int i = 0; i < Capacity; i++) floaters[i].active = false;
        }
    }
}
