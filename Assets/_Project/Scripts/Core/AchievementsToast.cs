using System.Collections.Generic;
using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Lightweight HUD that draws an "Achievement unlocked" toast whenever
    /// <see cref="Achievements.OnUnlocked"/> fires. Multiple unlocks queue
    /// behind one another so a single combo doesn't drop notifications.
    ///
    /// Auto-spawned via <see cref="RuntimeInitializeOnLoadMethodAttribute"/>;
    /// uses unscaled time so a paused (timeScale = 0) game still animates the
    /// toast cleanly.
    /// </summary>
    public class AchievementsToast : MonoBehaviour
    {
        private const float DisplayDuration = 3.5f;
        private const float FadeDuration = 0.45f;

        private static AchievementsToast _instance;

        private struct Pending
        {
            public Achievements.Definition Def;
            public float ShownAt;
        }

        private readonly Queue<Pending> queue = new();
        private Pending? active;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle iconStyle;
        private bool stylesReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[AchievementsToast]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AchievementsToast>();
        }

        private void OnEnable()
        {
            Achievements.OnUnlocked += HandleUnlocked;
        }

        private void OnDisable()
        {
            Achievements.OnUnlocked -= HandleUnlocked;
        }

        private void HandleUnlocked(Achievements.Definition def)
        {
            queue.Enqueue(new Pending { Def = def, ShownAt = -1f });
        }

        private void Update()
        {
            if (active == null && queue.Count > 0)
            {
                var next = queue.Dequeue();
                next.ShownAt = Time.unscaledTime;
                active = next;
            }
            else if (active != null && Time.unscaledTime - active.Value.ShownAt > DisplayDuration)
            {
                active = null;
            }
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, alignment = TextAnchor.MiddleLeft, wordWrap = true,
            };
            iconStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36, alignment = TextAnchor.MiddleCenter,
            };
            stylesReady = true;
        }

        private void OnGUI()
        {
            if (active == null) return;
            EnsureStyles();

            float age = Time.unscaledTime - active.Value.ShownAt;
            float alpha = 1f;
            if (age < FadeDuration)
                alpha = age / FadeDuration;
            else if (age > DisplayDuration - FadeDuration)
                alpha = Mathf.Max(0f, (DisplayDuration - age) / FadeDuration);

            float w = 380f, h = 88f;
            float x = (Screen.width - w) * 0.5f;
            float y = 24f;
            var rect = new Rect(x, y, w, h);

            var prevColor = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, 0.78f * alpha);
            GUI.Box(rect, GUIContent.none);

            GUI.color = new Color(1f, 0.84f, 0.2f, alpha); // gold accent line
            GUI.Box(new Rect(x, y, w, 3f), GUIContent.none);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(x + 8f, y + 8f, 72f, h - 16f), active.Value.Def.Icon, iconStyle);
            GUI.Label(new Rect(x + 88f, y + 8f, w - 96f, 28f), "Achievement Unlocked", titleStyle);
            GUI.Label(new Rect(x + 88f, y + 36f, w - 96f, 22f), active.Value.Def.Title, bodyStyle);
            GUI.color = new Color(0.85f, 0.85f, 0.85f, alpha);
            GUI.Label(new Rect(x + 88f, y + 56f, w - 96f, 26f), active.Value.Def.Description, bodyStyle);

            GUI.color = prevColor;
        }
    }
}
