using UnityEngine;
using UnityEngine.InputSystem;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Real-time debug overlay showing game state. Toggle with F11.
    /// Always-on compact display in top-right corner.
    /// </summary>
    public class DebugOverlay : MonoBehaviour
    {
        private static DebugOverlay instance;
        private bool isVisible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (instance == null)
            {
                var go = new GameObject("[DebugOverlay]");
                go.AddComponent<DebugOverlay>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public static void SetVisible(bool vis)
        {
            if (instance != null) instance.isVisible = vis;
        }

        public static void Toggle()
        {
            if (instance != null) instance.isVisible = !instance.isVisible;
        }

        private void Update()
        {
            if (Keyboard.current == null) return;
            if (Keyboard.current.f11Key.wasPressedThisFrame)
                isVisible = !isVisible;
        }

        private void OnGUI()
        {
            if (!isVisible) return;

            float w = 320f;
            float x = Screen.width - w - 10;
            float y = 10f;

            // Background
            GUI.color = new Color(0.02f, 0.02f, 0.05f, 0.8f);
            GUI.DrawTexture(new Rect(x - 4, y - 4, w + 8, 200), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                richText = true,
                padding = new RectOffset(4, 4, 1, 1)
            };

            float lineH = 16f;
            float cy = y;

            // Header
            DrawLine(ref cy, x, w, lineH, style,
                "<color=#00ffff><b>[F11] Game Logic Monitor</b></color>");

            // Rabbit state
            var rabbit = FindAnyObjectByType<Player.RabbitController>();
            if (rabbit != null)
            {
                string alive = rabbit.IsAlive ? "<color=#00ff00>✓</color>" : "<color=#ff0000>DEAD</color>";
                string fly = rabbit.IsFlying ? "<color=#00ffff>FLY</color>" : "run";
                string gnd = rabbit.IsGrounded ? "gnd" : "<color=#ffff00>air</color>";
                string stm = rabbit.IsStumbling ? "<color=#ff8800>STUMBLE</color>" : "ok";
                string god = CheatConsole.GodMode ? " <color=#ffff00>[GOD]</color>" : "";
                DrawLine(ref cy, x, w, lineH, style,
                    $"<color=#88ff88>🐇</color> SPD:{rabbit.CurrentSpeed:F1} LN:{rabbit.CurrentLane} {gnd} {fly} {stm} {alive}{god}");
            }
            else
            {
                DrawLine(ref cy, x, w, lineH, style, "<color=#ff4444>🐇 No rabbit</color>");
            }

            // Farmer state
            var farmer = FindAnyObjectByType<Enemies.FarmerController>();
            if (farmer != null && farmer.gameObject.activeSelf)
            {
                string state;
                Color stateCol;
                if (farmer.IsCatching) { state = "CATCH"; stateCol = Color.red; }
                else if (farmer.IsReappearing) { state = $"REAPPEAR({farmer.TooFarTimer:F1}s)"; stateCol = Color.yellow; }
                else if (!farmer.IsVisible) { state = "INVISIBLE"; stateCol = Color.gray; }
                else if (farmer.IsStumbling) { state = $"STUMBLE({farmer.StumbleTimer:F1}s)"; stateCol = Color.yellow; }
                else { state = "chasing"; stateCol = Color.green; }

                string vis = farmer.IsVisible ? "<color=#00ff00>vis</color>" : "<color=#666666>hid</color>";
                string stateStr = $"<color=#{ColorUtility.ToHtmlStringRGB(stateCol)}>{state}</color>";

                DrawLine(ref cy, x, w, lineH, style,
                    $"<color=#ff8888>👨‍🌾</color> DST:{farmer.CurrentDistance:F1}/{farmer.BaseDistance:F0} {stateStr} {vis}");
                DrawLine(ref cy, x, w, lineH, style,
                    $"   LN:{farmer.TargetLane} stumbles:{farmer.StumbleCount} threat:{farmer.NormalizedThreat:P0}");

                // Distance bar
                float distRatio = Mathf.Clamp01(farmer.CurrentDistance / farmer.MaxDistance);
                float fadeRatio = farmer.FadeDistance / farmer.MaxDistance;
                DrawDistanceBar(x + 4, cy, w - 8, 8, distRatio, fadeRatio);
                cy += 12;
            }
            else
            {
                DrawLine(ref cy, x, w, lineH, style, "<color=#666666>👨‍🌾 Farmer OFF</color>");
            }

            // Flight state
            var fc = rabbit != null ? rabbit.GetComponent<PowerUps.FlightController>() : null;
            if (fc != null)
            {
                DrawLine(ref cy, x, w, lineH, style,
                    "<color=#00ffff>🪽 FLIGHT ACTIVE</color>");
            }
            else
            {
                DrawLine(ref cy, x, w, lineH, style, "<color=#666666>🪽 no flight</color>");
            }

            // Baby rabbits
            int babyCount = PowerUps.BabyRabbit.ActiveBabies.Count;
            if (babyCount > 0)
                DrawLine(ref cy, x, w, lineH, style,
                    $"<color=#ff88ff>🍼 {babyCount} babies active</color>");

            // World stats
            var chunk = FindAnyObjectByType<World.ChunkManager>();
            if (chunk != null)
            {
                DrawLine(ref cy, x, w, lineH, style,
                    $"<color=#888888>📊 dist:{chunk.TotalDistance:F0}m chunks:{chunk.ActiveChunkCount} shifts:{chunk.OriginShiftCount}</color>");
            }

            // Resize background to actual content
            float totalH = cy - y + 4;
            GUI.color = new Color(0.02f, 0.02f, 0.05f, 0.8f);
            GUI.DrawTexture(new Rect(x - 4, y - 4, w + 8, totalH + 4), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawLine(ref float cy, float x, float w, float h, GUIStyle style, string text)
        {
            GUI.Label(new Rect(x, cy, w, h), text, style);
            cy += h;
        }

        private void DrawDistanceBar(float x, float y, float w, float h, float fillRatio, float fadeMarker)
        {
            // Background
            GUI.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

            // Fill (green → yellow → red)
            Color barColor = fillRatio < 0.3f ? Color.green
                : fillRatio < 0.7f ? Color.yellow : Color.red;
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(x, y, w * fillRatio, h), Texture2D.whiteTexture);

            // Fade distance marker
            GUI.color = Color.white;
            float markerX = x + w * fadeMarker;
            GUI.DrawTexture(new Rect(markerX, y, 2, h), Texture2D.whiteTexture);

            GUI.color = Color.white;
        }
    }
}
