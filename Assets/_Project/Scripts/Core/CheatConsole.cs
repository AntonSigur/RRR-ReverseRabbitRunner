using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// In-game debug console. Shift+F12 to toggle.
    /// Works in builds, not just the editor. Type 'help' for commands.
    /// </summary>
    public class CheatConsole : MonoBehaviour
    {
        private static CheatConsole instance;

        private bool isOpen;
        private string inputText = "";
        private readonly List<LogEntry> log = new();
        private Vector2 scrollPos;
        private readonly List<string> history = new();
        private int historyIndex = -1;
        private bool focusInput;
        private bool submitRequested;

        private Dictionary<string, System.Action<string[]>> commands;

        // Cheat states accessible by other scripts
        public static bool GodMode { get; private set; }
        public static float? SpeedOverride { get; private set; }

        private struct LogEntry
        {
            public string text;
            public Color color;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (instance == null)
            {
                var go = new GameObject("[CheatConsole]");
                go.AddComponent<CheatConsole>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            RegisterCommands();
        }

        private void RegisterCommands()
        {
            commands = new Dictionary<string, System.Action<string[]>>
            {
                ["help"]    = _ => ShowHelp(),
                ["clear"]   = _ => log.Clear(),
                ["babies"]  = SpawnBabies,
                ["baby"]    = SpawnBabies,
                ["kill"]    = _ => KillBabies(),
                ["god"]     = _ => ToggleGod(),
                ["speed"]   = SetSpeed,
                ["score"]   = SetScore,
                ["farmer"]  = _ => ToggleFarmer(),
                ["fstatus"] = _ => FarmerStatus(),
                ["fdist"]   = SetFarmerDistance,
                ["fstumble"]= _ => FarmerForceStumble(),
                ["die"]     = _ => TriggerDeath(),
                ["wing"]    = _ => TriggerWingCarrot(),
                ["fly"]     = _ => TriggerWingCarrot(),
                ["test"]    = RunTest,
                ["overlay"] = _ => DebugOverlay.Toggle(),
            };
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.f12Key.wasPressedThisFrame &&
                (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed))
            {
                isOpen = !isOpen;
                if (isOpen)
                {
                    inputText = "";
                    focusInput = true;
                }
            }

            // Reliable Enter key handling via Input System (IMGUI events can be flaky)
            if (isOpen && Keyboard.current.enterKey.wasPressedThisFrame)
                submitRequested = true;
        }

        private void OnGUI()
        {
            if (!isOpen) return;

            float w = Screen.width;
            float h = 160f; // compact: ~7 log lines + input
            float y = Screen.height - h;

            // Dark background
            GUI.color = new Color(0.05f, 0.05f, 0.1f, 0.75f);
            GUI.DrawTexture(new Rect(0, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(8, y + 4, w - 16, h - 8));

            // Header
            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.cyan }
            };
            GUILayout.Label("RRR Debug Console  |  Shift+F12 to close  |  'help' for commands", headerStyle);

            // Active cheat indicators
            if (GodMode || SpeedOverride.HasValue)
            {
                var indStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    normal = { textColor = Color.yellow }
                };
                string ind = "";
                if (GodMode) ind += "[GOD] ";
                if (SpeedOverride.HasValue) ind += $"[SPD:{SpeedOverride.Value:F0}] ";
                GUILayout.Label(ind, indStyle);
            }

            // Log area
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(h - 58));

            var logStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                padding = new RectOffset(2, 2, 0, 0)
            };

            foreach (var entry in log)
            {
                GUI.contentColor = entry.color;
                GUILayout.Label(entry.text, logStyle);
            }
            GUI.contentColor = Color.white;

            GUILayout.EndScrollView();

            // Input line
            GUILayout.BeginHorizontal();

            GUI.SetNextControlName("CheatInput");
            var inputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 13,
                normal = { textColor = Color.green }
            };
            inputText = GUILayout.TextField(inputText, inputStyle, GUILayout.Height(22));

            // Handle Enter via Input System flag (more reliable than IMGUI events)
            if (submitRequested)
            {
                SubmitInput();
                submitRequested = false;
            }

            // Arrow keys and Escape via IMGUI events
            Event e = Event.current;
            if (GUI.GetNameOfFocusedControl() == "CheatInput" && e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    SubmitInput();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.UpArrow && history.Count > 0)
                {
                    historyIndex = Mathf.Max(0, historyIndex - 1);
                    inputText = history[historyIndex];
                    e.Use();
                }
                else if (e.keyCode == KeyCode.DownArrow && history.Count > 0)
                {
                    historyIndex = Mathf.Min(history.Count, historyIndex + 1);
                    inputText = historyIndex < history.Count ? history[historyIndex] : "";
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    isOpen = false;
                    e.Use();
                }
            }

            if (GUILayout.Button("Run", GUILayout.Width(50), GUILayout.Height(22)))
                SubmitInput();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (focusInput)
            {
                GUI.FocusControl("CheatInput");
                focusInput = false;
            }
        }

        private void SubmitInput()
        {
            if (string.IsNullOrWhiteSpace(inputText)) return;
            string cmd = inputText.Trim();
            ExecuteCommand(cmd);
            history.Add(cmd);
            historyIndex = history.Count;
            inputText = "";
        }

        private void ExecuteCommand(string input)
        {
            Log($"> {input}", Color.yellow);

            var parts = input.Split(' ');
            var cmd = parts[0].ToLower();
            var args = parts.Skip(1).ToArray();

            if (commands.TryGetValue(cmd, out var action))
            {
                try { action(args); }
                catch (System.Exception ex)
                {
                    Log($"Error: {ex.Message}", Color.red);
                }
            }
            else
            {
                Log($"Unknown: '{cmd}'. Type 'help'.", Color.red);
            }
        }

        private void Log(string text, Color color)
        {
            log.Add(new LogEntry { text = text, color = color });
            if (log.Count > 300) log.RemoveRange(0, log.Count - 200);
            scrollPos.y = float.MaxValue;
        }

        // ======================== Commands ========================

        private void ShowHelp()
        {
            Log("=== RRR Debug Console ===", Color.cyan);
            Log("babies [n]   Spawn n rainbow babies (default: 125)", Color.white);
            Log("kill         Kill all baby rabbits", Color.white);
            Log("god          Toggle invincibility", Color.white);
            Log("speed [n]    Set speed (0 or no arg = show/reset)", Color.white);
            Log("score [n]    Set score (no arg = show)", Color.white);
            Log("farmer       Toggle farmer on/off", Color.white);
            Log("fstatus      Show farmer state details", Color.white);
            Log("fdist [n]    Set farmer distance", Color.white);
            Log("fstumble     Force farmer stumble", Color.white);
            Log("die          Trigger instant death", Color.white);
            Log("wing/fly     Activate Wing-Carrot flight", Color.white);
            Log("test <name>  Run test (flight/farmer/stumble/distance/all)", Color.white);
            Log("overlay      Toggle debug overlay (or F11)", Color.white);
            Log("clear        Clear console", Color.white);
            Log("help         Show this", Color.white);
        }

        private void SpawnBabies(string[] args)
        {
            int count = 125;
            if (args.Length > 0 && int.TryParse(args[0], out int c))
                count = Mathf.Clamp(c, 1, 500);

            // Kill existing
            foreach (var baby in new List<PowerUps.BabyRabbit>(PowerUps.BabyRabbit.ActiveBabies))
                if (baby != null) Destroy(baby.gameObject);
            PowerUps.BabyRabbit.ActiveBabies.Clear();

            var rabbit = FindFirstObjectByType<Player.RabbitController>();
            if (rabbit == null) { Log("No rabbit found!", Color.red); return; }

            var farmer = FindFirstObjectByType<Enemies.FarmerController>();
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");

            for (int i = 0; i < count; i++)
            {
                PowerUps.BabyRabbit.CreateBabyRabbit(i, rabbit, farmer,
                    Random.Range(-5f, 10f),
                    Random.Range(-7f, 7f),
                    Random.Range(0.8f, 1.2f),
                    urpLit);
            }

            Log($"Spawned {count} rainbow babies!", Color.green);
        }

        private void KillBabies()
        {
            int count = PowerUps.BabyRabbit.ActiveBabies.Count;
            foreach (var baby in new List<PowerUps.BabyRabbit>(PowerUps.BabyRabbit.ActiveBabies))
                if (baby != null) Destroy(baby.gameObject);
            PowerUps.BabyRabbit.ActiveBabies.Clear();
            Log($"Killed {count} babies.", count > 0 ? Color.yellow : Color.gray);
        }

        private void ToggleGod()
        {
            GodMode = !GodMode;
            Log($"God mode: {(GodMode ? "ON — invincible!" : "OFF")}", GodMode ? Color.green : Color.red);
        }

        private void SetSpeed(string[] args)
        {
            var rabbit = FindFirstObjectByType<Player.RabbitController>();
            if (rabbit == null) { Log("No rabbit found!", Color.red); return; }

            if (args.Length == 0)
            {
                string mode = SpeedOverride.HasValue ? $" (override: {SpeedOverride.Value:F0})" : "";
                Log($"Speed: {rabbit.CurrentSpeed:F1}{mode}", Color.white);
                return;
            }

            if (float.TryParse(args[0], out float spd))
            {
                if (spd <= 0)
                {
                    SpeedOverride = null;
                    Log("Speed override cleared — back to normal.", Color.yellow);
                }
                else
                {
                    SpeedOverride = spd;
                    Log($"Speed locked to {spd:F0}.", Color.green);
                }
            }
        }

        private void SetScore(string[] args)
        {
            var sm = ScoreManager.Instance;
            if (sm == null) { Log("No ScoreManager!", Color.red); return; }

            if (args.Length == 0)
            {
                Log($"Score: {sm.CurrentScore}  High: {sm.HighScore}", Color.white);
                return;
            }

            if (int.TryParse(args[0], out int val))
            {
                sm.ResetScore();
                if (val > 0) sm.AddScore(val);
                Log($"Score set to {val}.", Color.green);
            }
        }

        private void ToggleFarmer()
        {
            var farmer = FindFirstObjectByType<Enemies.FarmerController>(FindObjectsInactive.Include);
            if (farmer == null) { Log("No farmer found!", Color.red); return; }

            bool newState = !farmer.gameObject.activeSelf;
            farmer.gameObject.SetActive(newState);
            Log($"Farmer: {(newState ? "ON" : "OFF")}", newState ? Color.red : Color.green);
        }

        private void TriggerDeath()
        {
            var rabbit = FindFirstObjectByType<Player.RabbitController>();
            if (rabbit == null) { Log("No rabbit found!", Color.red); return; }

            if (!rabbit.IsAlive) { Log("Rabbit already dead!", Color.gray); return; }

            // Move farmer next to rabbit for dramatic death
            var farmerObj = GameObject.FindGameObjectWithTag("Farmer");
            if (farmerObj != null)
                farmerObj.transform.position = rabbit.transform.position + Vector3.forward * 1.5f;

            rabbit.Die();
            Log("Rabbit killed!", Color.red);
        }

        private void TriggerWingCarrot()
        {
            var rabbit = FindFirstObjectByType<Player.RabbitController>();
            if (rabbit == null) { Log("No rabbit found!", Color.red); return; }

            if (rabbit.IsFlying) { Log("Already flying!", Color.yellow); return; }

            var fc = rabbit.gameObject.AddComponent<PowerUps.FlightController>();
            fc.Initialize(rabbit, 6f, 8f);
            Log("Wing-Carrot! FLIGHT MODE!", Color.cyan);
        }

        private void FarmerStatus()
        {
            var farmer = FindFirstObjectByType<Enemies.FarmerController>(FindObjectsInactive.Include);
            if (farmer == null) { Log("No farmer found!", Color.red); return; }

            Log("=== Farmer Status ===", Color.cyan);
            Log($"Active: {farmer.gameObject.activeSelf}", Color.white);
            Log($"Distance: {farmer.CurrentDistance:F1} / base:{farmer.BaseDistance:F0}", Color.white);
            Log($"Visible: {farmer.IsVisible} (fade at {farmer.FadeDistance})", Color.white);
            Log($"Stumbling: {farmer.IsStumbling} (timer: {farmer.StumbleTimer:F1}s)", Color.white);
            Log($"Reappearing: {farmer.IsReappearing} (timer: {farmer.TooFarTimer:F1}s)", Color.white);
            Log($"Catching: {farmer.IsCatching}", Color.white);
            Log($"Lane: {farmer.TargetLane}  Stumbles: {farmer.StumbleCount}", Color.white);
            Log($"Threat: {farmer.NormalizedThreat:P0}", Color.white);
        }

        private void SetFarmerDistance(string[] args)
        {
            var farmer = FindFirstObjectByType<Enemies.FarmerController>();
            if (farmer == null) { Log("No farmer found!", Color.red); return; }

            if (args.Length == 0)
            {
                Log($"Farmer distance: {farmer.CurrentDistance:F1}", Color.white);
                return;
            }

            if (float.TryParse(args[0], out float dist))
            {
                farmer.ForceDistance(dist);
                Log($"Farmer distance set to {dist:F1}", Color.green);
            }
        }

        private void FarmerForceStumble()
        {
            var farmer = FindFirstObjectByType<Enemies.FarmerController>();
            if (farmer == null) { Log("No farmer found!", Color.red); return; }
            farmer.ForceStumble();
            Log($"Farmer stumbled! Distance: {farmer.CurrentDistance:F1}", Color.yellow);
        }

        private void RunTest(string[] args)
        {
            if (args.Length == 0)
            {
                Log("Usage: test <name>", Color.yellow);
                Log("Tests: flight, farmer, stumble, distance, all", Color.white);
                return;
            }

            var runner = GameTestRunner.Instance;
            if (runner == null)
            {
                Log("TestRunner not available!", Color.red);
                return;
            }

            runner.RunTest(args[0], Log);
        }
    }
}
