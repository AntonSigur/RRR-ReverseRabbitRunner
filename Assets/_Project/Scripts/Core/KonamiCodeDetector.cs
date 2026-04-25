using UnityEngine;
using UnityEngine.InputSystem;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Detects the Konami code (↑ ↑ ↓ ↓ ← → ← → B A) on the keyboard. On a
    /// successful entry, toggles <see cref="EasterEggs.GoldenRabbitUnlocked"/>
    /// and emits a brief on-screen toast.
    ///
    /// Runs everywhere — auto-spawned at runtime as a DontDestroyOnLoad
    /// singleton, so the cheat is always discoverable. Implementation is
    /// timing-tolerant (no per-step deadline) but resets the sequence on the
    /// first wrong key press, preventing accidental fragments from arming the
    /// cheat. Uses the new Input System exclusively (Input System package is
    /// already a dependency).
    /// </summary>
    public class KonamiCodeDetector : MonoBehaviour
    {
        private static KonamiCodeDetector instance;

        private static readonly Key[] Sequence =
        {
            Key.UpArrow,    Key.UpArrow,
            Key.DownArrow,  Key.DownArrow,
            Key.LeftArrow,  Key.RightArrow,
            Key.LeftArrow,  Key.RightArrow,
            Key.B,          Key.A,
        };

        private int progress;
        private float toastUntilTime;
        private string toastMessage;
        private GUIStyle toastStyle;
        private float lastKeyTime;
        private const float SequenceTimeoutSeconds = 4f; // resets if user pauses too long

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("[KonamiCodeDetector]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<KonamiCodeDetector>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Time-based reset — keeps fragments from accumulating across long sessions.
            if (progress > 0 && Time.unscaledTime - lastKeyTime > SequenceTimeoutSeconds)
                progress = 0;

            // Find the single key pressed this frame (if any). We only consider
            // keys present in the sequence so other typing doesn't reset us
            // every keystroke — but if a SEQUENCE-key is pressed wrongly, that
            // does reset, which is the correct UX for a code.
            Key pressed = Key.None;
            for (int i = 0; i < AllRelevantKeys.Length; i++)
            {
                var k = AllRelevantKeys[i];
                if (kb[k].wasPressedThisFrame) { pressed = k; break; }
            }
            if (pressed == Key.None) return;

            lastKeyTime = Time.unscaledTime;

            if (pressed == Sequence[progress])
            {
                progress++;
                if (progress >= Sequence.Length)
                {
                    progress = 0;
                    OnSequenceComplete();
                }
            }
            else
            {
                // Restart, but allow the wrong key to count if it matches the
                // sequence's first step (common case: user starts over).
                progress = (pressed == Sequence[0]) ? 1 : 0;
            }
        }

        // The set of keys we listen for at all. Pressing anything outside this
        // set leaves the sequence progress untouched, so normal typing in
        // text fields won't break the code halfway through.
        private static readonly Key[] AllRelevantKeys =
        {
            Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow, Key.A, Key.B,
        };

        private void OnSequenceComplete()
        {
            bool nowOn = !EasterEggs.GoldenRabbitUnlocked;
            EasterEggs.GoldenRabbitUnlocked = nowOn;

            toastMessage = nowOn
                ? "✨🥕 GOLDEN RABBIT MODE — UNLOCKED 🥕✨\n+50% score • restart run to see the tint"
                : "Golden Rabbit Mode — OFF";
            toastUntilTime = Time.unscaledTime + 3.5f;

            // Best-effort SFX feedback — manager may be absent in main menu.
            AudioManager.Instance?.PlayMenuClick();
        }

        private void OnGUI()
        {
            if (Time.unscaledTime > toastUntilTime || string.IsNullOrEmpty(toastMessage))
                return;

            if (toastStyle == null)
            {
                toastStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 26,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true,
                };
            }

            // Fade-out in the last 0.6s.
            float remaining = toastUntilTime - Time.unscaledTime;
            float alpha = Mathf.Clamp01(remaining / 0.6f);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.85f, 0.2f, alpha);

            // Drop-shadow for readability against any background.
            var rect = new Rect(0, Screen.height * 0.18f, Screen.width, 80);
            var shadowRect = new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height);
            Color shadow = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f * alpha);
            GUI.Label(shadowRect, toastMessage, toastStyle);
            GUI.color = shadow;
            GUI.Label(rect, toastMessage, toastStyle);

            GUI.color = prev;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
