using UnityEngine;
using System.Collections;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Automated game logic test runner. Runs coroutine-based test scenarios
    /// and reports PASS/FAIL to the cheat console log.
    /// </summary>
    public class GameTestRunner : MonoBehaviour
    {
        private static GameTestRunner instance;
        private System.Action<string, Color> log;
        private bool isRunning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (instance == null)
            {
                var go = new GameObject("[GameTestRunner]");
                go.AddComponent<GameTestRunner>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public static GameTestRunner Instance => instance;

        public void RunTest(string name, System.Action<string, Color> logFn)
        {
            log = logFn;
            if (isRunning) { log("A test is already running!", Color.red); return; }

            switch (name.ToLower())
            {
                case "flight":   StartCoroutine(TestFlight()); break;
                case "farmer":   StartCoroutine(TestFarmerRecovery()); break;
                case "stumble":  StartCoroutine(TestStumbleChain()); break;
                case "distance": StartCoroutine(TestDistanceLogic()); break;
                case "all":      StartCoroutine(TestAll()); break;
                default:
                    log($"Unknown test: '{name}'", Color.red);
                    log("Available: flight, farmer, stumble, distance, all", Color.white);
                    break;
            }
        }

        private void Log(string msg, Color c) => log?.Invoke(msg, c);
        private void Header(string name) => Log($"━━━ TEST: {name} ━━━", Color.cyan);
        private void Pass(string msg) => Log($"  ✅ PASS: {msg}", Color.green);
        private void Fail(string msg) => Log($"  ❌ FAIL: {msg}", Color.red);
        private void Info(string msg) => Log($"  ℹ {msg}", Color.gray);

        private void Assert(string label, bool condition, string detail = "")
        {
            if (condition)
                Pass($"{label} {detail}");
            else
                Fail($"{label} {detail}");
        }

        // ===================== TEST: Flight + Farmer =====================

        private IEnumerator TestFlight()
        {
            isRunning = true;
            Header("Flight + Farmer Preservation");

            var rabbit = FindAnyObjectByType<Player.RabbitController>();
            var farmer = FindAnyObjectByType<Enemies.FarmerController>();

            if (rabbit == null || farmer == null)
            {
                Fail("Missing rabbit or farmer!"); isRunning = false; yield break;
            }

            // Enable god mode for safety
            bool wasGod = CheatConsole.GodMode;
            if (!wasGod) CheatConsole.GodMode = true;

            float preDist = farmer.CurrentDistance;
            int preStumbles = farmer.StumbleCount;
            Info($"Pre-flight: dist={preDist:F1}, stumbles={preStumbles}");

            // Trigger flight
            var fc = rabbit.gameObject.AddComponent<PowerUps.FlightController>();
            fc.Initialize(rabbit, 6f, 8f);
            Info("Flight triggered...");

            // Monitor during flight (log every 2 seconds)
            float elapsed = 0;
            float lastLog = 0;
            while (rabbit.IsFlying)
            {
                elapsed += Time.deltaTime;
                if (elapsed - lastLog >= 2f)
                {
                    lastLog = elapsed;
                    Info($"  t={elapsed:F0}s | dist={farmer.CurrentDistance:F1} stumble={farmer.IsStumbling} vis={farmer.IsVisible}");
                }
                yield return null;
            }

            Info($"Flight ended after {elapsed:F1}s");

            // Wait 3 seconds for post-flight settling
            yield return new WaitForSeconds(3f);

            float postDist = farmer.CurrentDistance;
            int postStumbles = farmer.StumbleCount;
            bool farmerActive = farmer.gameObject.activeSelf;
            bool farmerVisible = farmer.IsVisible;

            Info($"Post-flight: dist={postDist:F1}, stumbles={postStumbles}, active={farmerActive}, visible={farmerVisible}");

            // Assertions
            Assert("Farmer active", farmerActive);
            Assert("Farmer visible", farmerVisible, $"(dist={postDist:F1} < fade={farmer.FadeDistance})");
            Assert("No new stumbles during flight", postStumbles == preStumbles,
                $"(pre={preStumbles} post={postStumbles})");
            Assert("Distance reasonable", postDist < 15f,
                $"(dist={postDist:F1}, should be near base={farmer.BaseDistance})");

            if (!wasGod) CheatConsole.GodMode = false;
            isRunning = false;
        }

        // ===================== TEST: Farmer Recovery =====================

        private IEnumerator TestFarmerRecovery()
        {
            isRunning = true;
            Header("Farmer Recovery Rate");

            var farmer = FindAnyObjectByType<Enemies.FarmerController>();
            if (farmer == null) { Fail("No farmer!"); isRunning = false; yield break; }

            bool wasGod = CheatConsole.GodMode;
            CheatConsole.GodMode = true;

            float baseDist = farmer.BaseDistance;
            Info($"Base distance: {baseDist:F1}");

            // Force stumble
            farmer.ForceStumble();
            yield return null; // Wait one frame

            float afterStumble = farmer.CurrentDistance;
            Info($"After stumble: {afterStumble:F1}");
            Assert("Stumble added penalty", afterStumble > baseDist + 5f,
                $"(dist={afterStumble:F1})");

            // Measure recovery over 5 seconds
            float startDist = farmer.CurrentDistance;
            yield return new WaitForSeconds(5f);
            float endDist = farmer.CurrentDistance;

            float recovered = startDist - endDist;
            float rate = recovered / 5f;
            Info($"Recovery: {startDist:F1} → {endDist:F1} ({recovered:F1} in 5s, rate={rate:F2}/s)");

            Assert("Distance decreased", endDist < startDist, $"({startDist:F1} → {endDist:F1})");
            Assert("Recovery rate > 0", rate > 0.05f, $"(rate={rate:F2}/s)");

            CheatConsole.GodMode = wasGod;
            isRunning = false;
        }

        // ===================== TEST: Stumble Chain =====================

        private IEnumerator TestStumbleChain()
        {
            isRunning = true;
            Header("Stumble Chain (Multiple Hits)");

            var farmer = FindAnyObjectByType<Enemies.FarmerController>();
            if (farmer == null) { Fail("No farmer!"); isRunning = false; yield break; }

            bool wasGod = CheatConsole.GodMode;
            CheatConsole.GodMode = true;

            // Reset to base
            farmer.ForceDistance(farmer.BaseDistance);
            yield return null;

            Info($"Starting distance: {farmer.CurrentDistance:F1}");

            // Stumble 3 times with cooldown
            for (int i = 0; i < 3; i++)
            {
                // Wait for stumble cooldown
                while (farmer.IsStumbling) yield return null;

                farmer.ForceStumble();
                yield return null;
                Info($"Stumble {i + 1}: dist={farmer.CurrentDistance:F1} vis={farmer.IsVisible}");
            }

            float peakDist = farmer.CurrentDistance;
            Assert("Distance accumulated", peakDist > farmer.BaseDistance + 20f,
                $"(dist={peakDist:F1})");

            // Check if farmer eventually fades
            bool didFade = !farmer.IsVisible;
            Info($"Faded out: {didFade} (dist={peakDist:F1}, fade={farmer.FadeDistance})");

            // Wait for reappear
            if (didFade || farmer.IsReappearing)
            {
                Info("Waiting for reappear...");
                float waitStart = Time.time;
                while ((farmer.IsReappearing || !farmer.IsVisible) && Time.time - waitStart < 15f)
                    yield return null;

                float waitTime = Time.time - waitStart;
                Assert("Farmer reappeared", farmer.IsVisible, $"(waited {waitTime:F1}s)");
                Assert("Distance reset to base", farmer.CurrentDistance < farmer.BaseDistance + 2f,
                    $"(dist={farmer.CurrentDistance:F1})");
            }

            CheatConsole.GodMode = wasGod;
            isRunning = false;
        }

        // ===================== TEST: Distance Logic =====================

        private IEnumerator TestDistanceLogic()
        {
            isRunning = true;
            Header("Distance Logic (MoveTowards)");

            var farmer = FindAnyObjectByType<Enemies.FarmerController>();
            if (farmer == null) { Fail("No farmer!"); isRunning = false; yield break; }

            bool wasGod = CheatConsole.GodMode;
            CheatConsole.GodMode = true;

            // Test 1: Recovery from high distance (the old Mathf.Min bug)
            farmer.ForceDistance(20f);
            yield return null;
            float d1 = farmer.CurrentDistance;
            yield return new WaitForSeconds(0.1f);
            float d2 = farmer.CurrentDistance;

            Assert("No instant snap", d2 > farmer.BaseDistance + 1f,
                $"(20→{d2:F1} after 0.1s, NOT {farmer.BaseDistance})");
            Assert("Gradual decrease", d2 < d1, $"({d1:F1}→{d2:F1})");

            // Test 2: Recovery from low distance
            farmer.ForceDistance(2f);
            yield return null;
            float d3 = farmer.CurrentDistance;
            yield return new WaitForSeconds(0.5f);
            float d4 = farmer.CurrentDistance;

            Assert("Increases from low", d4 > d3, $"({d3:F1}→{d4:F1})");

            // Test 3: Stable at base
            farmer.ForceDistance(farmer.BaseDistance);
            yield return new WaitForSeconds(0.5f);
            float d5 = farmer.CurrentDistance;
            Assert("Stable at base", Mathf.Abs(d5 - farmer.BaseDistance) < 0.1f,
                $"(dist={d5:F1}, base={farmer.BaseDistance})");

            CheatConsole.GodMode = wasGod;
            isRunning = false;
        }

        // ===================== TEST: Run All =====================

        private IEnumerator TestAll()
        {
            Log("═══ RUNNING ALL TESTS ═══", Color.cyan);

            yield return StartCoroutine(TestDistanceLogic());
            yield return new WaitForSeconds(1f);

            yield return StartCoroutine(TestFarmerRecovery());
            yield return new WaitForSeconds(1f);

            yield return StartCoroutine(TestStumbleChain());
            yield return new WaitForSeconds(1f);

            yield return StartCoroutine(TestFlight());

            Log("═══ ALL TESTS COMPLETE ═══", Color.cyan);
        }
    }
}
