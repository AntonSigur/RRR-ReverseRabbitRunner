using UnityEngine;
using UnityEngine.InputSystem;

namespace ReverseRabbitRunner.UI
{
    /// <summary>
    /// Simple in-game HUD showing score, speed, and farmer distance.
    /// Also handles pause menu (Esc/Q) using OnGUI.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Player.RabbitController rabbit;
        [SerializeField] private Enemies.FarmerController farmer;

        // Cached singleton-style references — looked up once instead of per-frame in OnGUI
        private Core.DeathSequence cachedDeathSeq;
        private World.ChunkManager cachedChunkMgr;

        // Combo display state
        private GUIStyle comboStyle;
        private GUIStyle comboBigStyle;
        private float multiplierFlashTimer;  // tier-up celebration flash
        private float nearMissFlashTimer;    // "NICE!" pop on dodge
        private float tierUpFlashTimer;      // "TIER X" celebration on difficulty step-up

        private GUIStyle scoreStyle;
        private GUIStyle warningStyle;
        private GUIStyle infoStyle;
        private GUIStyle gameOverStyle;
        private GUIStyle buttonStyle;
        private bool stylesInitialized = false;
        private bool isPaused = false;
        private bool showSettings = false;
        private bool showAchievements = false;
        private Vector2 achievementsScroll;
        private bool wasStumbling;
        private float stumbleFlashTimer;

        // Death replay (top-down) state.
        private bool replayActive;
        private float replayStartedAt;
        private const float ReplayPlaybackDuration = 2.4f;

        private void Start()
        {
            if (rabbit == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) rabbit = playerObj.GetComponent<Player.RabbitController>();
            }
            if (farmer == null)
            {
                var farmerObj = GameObject.FindGameObjectWithTag("Farmer");
                if (farmerObj != null) farmer = farmerObj.GetComponent<Enemies.FarmerController>();
            }

            cachedDeathSeq = FindAnyObjectByType<Core.DeathSequence>();
            cachedChunkMgr = FindAnyObjectByType<World.ChunkManager>();

            // Subscribe to combo events for the tier-up celebration animation.
            // Subscription survives across sceneLoad because GameHUD itself is per-scene
            // (re-Start runs on a fresh instance).
            if (Core.ScoreManager.Instance != null)
                Core.ScoreManager.Instance.OnComboChanged += OnComboChanged;

            // Wire rabbit feedback events to camera shake + HUD pop
            if (rabbit != null)
            {
                rabbit.OnStumble += OnRabbitStumble;
                rabbit.OnNearMiss += OnRabbitNearMiss;
            }

            if (Core.DifficultyManager.Instance != null)
                Core.DifficultyManager.Instance.OnTierUp += OnDifficultyTierUp;
        }

        private void OnDestroy()
        {
            if (Core.ScoreManager.Instance != null)
                Core.ScoreManager.Instance.OnComboChanged -= OnComboChanged;
            if (rabbit != null)
            {
                rabbit.OnStumble -= OnRabbitStumble;
                rabbit.OnNearMiss -= OnRabbitNearMiss;
            }
            if (Core.DifficultyManager.Instance != null)
                Core.DifficultyManager.Instance.OnTierUp -= OnDifficultyTierUp;
        }

        private void OnDifficultyTierUp(int newTier)
        {
            tierUpFlashTimer = 1.6f;
            Player.CameraFollow.Instance?.Shake(0.18f, 0.25f);
        }

        private void OnComboChanged(int comboCount, int multiplier, bool tierIncreased)
        {
            if (tierIncreased)
            {
                multiplierFlashTimer = 1.2f;
                // Subtle "you levelled up" camera punch
                Player.CameraFollow.Instance?.Shake(0.15f, 0.22f);
            }
        }

        private void OnRabbitStumble(float penalty)
        {
            // Bigger shake for tall/heavy stumbles
            float intensity = penalty >= 3f ? 0.45f : 0.25f;
            Player.CameraFollow.Instance?.Shake(intensity, 0.30f);
        }

        private void OnRabbitNearMiss(GameObject obstacle)
        {
            Player.CameraFollow.Instance?.Shake(0.08f, 0.15f);
            nearMissFlashTimer = 0.7f;
            // Reward the dodge — half a carrot's worth, and it counts as a streak hit
            // so dodging masterfully also builds your combo.
            Core.ScoreManager.Instance?.AddScore(1);
        }

        private Core.DeathSequence GetDeathSeq()
        {
            // Re-resolve if scene reloaded and the cached instance was destroyed
            if (cachedDeathSeq == null) cachedDeathSeq = FindAnyObjectByType<Core.DeathSequence>();
            return cachedDeathSeq;
        }

        private World.ChunkManager GetChunkMgr()
        {
            if (cachedChunkMgr == null) cachedChunkMgr = FindAnyObjectByType<World.ChunkManager>();
            return cachedChunkMgr;
        }

        private static readonly Color[] ComboTierColors = new[]
        {
            new Color(1f, 1f, 1f),         // x1 (never shown — combo HUD hidden)
            new Color(1f, 0.95f, 0.4f),    // x2 yellow
            new Color(1f, 0.65f, 0.15f),   // x3 orange
            new Color(1f, 0.3f, 0.2f),     // x4 red
            new Color(1f, 0.3f, 0.95f),    // x5 magenta
        };

        private void DrawComboHUD(Core.ScoreManager score, float padding)
        {
            if (score == null || score.ComboCount <= 0) return;

            // Small streak label below score
            int multIdx = Mathf.Clamp(score.Multiplier - 1, 0, ComboTierColors.Length - 1);
            Color tierColor = ComboTierColors[multIdx];
            comboStyle.normal.textColor = tierColor;
            string streakText = score.Multiplier > 1
                ? $"🔥 x{score.Multiplier}  ({score.ComboCount} streak)"
                : $"🔥 {score.ComboCount} streak";
            // Below the existing Speed/Lane info lines
            GUI.Label(new Rect(padding, padding + 100, 320, 30), streakText, comboStyle);

            // Combo timer pill (thin bar showing how long until streak expires)
            float remaining = score.ComboTimeRemaining;
            float window = Mathf.Max(0.01f, score.ComboTimeWindow);
            float fill = Mathf.Clamp01(remaining / window);
            float barW = 220f;
            float barH = 4f;
            float barY = padding + 132f;
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(new Rect(padding, barY, barW, barH), Texture2D.whiteTexture);
            GUI.color = tierColor;
            GUI.DrawTexture(new Rect(padding, barY, barW * fill, barH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Tier-up celebration overlay
            if (multiplierFlashTimer > 0f && score.Multiplier > 1)
            {
                float t = Mathf.Clamp01(multiplierFlashTimer / 1.2f);
                // Fade out over the lifetime, big at start then settle
                float alpha = t;
                float scaleBoost = 0.5f + (1f - t) * 0.6f;     // 1.1 → 0.5
                float fontScale = 1f + scaleBoost;

                int oldFont = comboBigStyle.fontSize;
                comboBigStyle.fontSize = Mathf.RoundToInt(64 * fontScale);
                comboBigStyle.normal.textColor = new Color(tierColor.r, tierColor.g, tierColor.b, alpha);
                string bigText = $"x{score.Multiplier}  COMBO!";
                GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 120), bigText, comboBigStyle);
                comboBigStyle.fontSize = oldFont;
                comboBigStyle.normal.textColor = Color.white;
            }
        }

        private void DrawNearMissPop()
        {
            if (nearMissFlashTimer <= 0f) return;
            float t = Mathf.Clamp01(nearMissFlashTimer / 0.7f);
            float alpha = t;
            float yOffset = (1f - t) * -40f;  // floats upward as it fades

            int oldFont = comboBigStyle.fontSize;
            comboBigStyle.fontSize = 36;
            comboBigStyle.normal.textColor = new Color(0.6f, 1f, 0.4f, alpha);
            GUI.Label(new Rect(0, Screen.height * 0.55f + yOffset, Screen.width, 50),
                "NICE DODGE!  +1", comboBigStyle);
            comboBigStyle.fontSize = oldFont;
            comboBigStyle.normal.textColor = Color.white;
        }

        private void DrawMagnetHUD(float padding)
        {
            var magnet = PowerUps.MagnetEffect.Active;
            if (magnet == null) return;

            float frac = magnet.DurationFraction;
            float barW = 220f;
            float barH = 14f;
            float x = Screen.width - barW - padding;
            float y = padding + 80f;     // sits below the score panel

            // Background
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x - 4, y - 18, barW + 8, barH + 26), Texture2D.whiteTexture);

            // Label
            var label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold
            };
            label.normal.textColor = new Color(1f, 0.9f, 0.3f);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y - 18, barW, 18), $"\u2728 MAGNET  {magnet.Remaining:0.0}s", label);

            // Bar background
            GUI.color = new Color(0.2f, 0.18f, 0.05f, 0.9f);
            GUI.DrawTexture(new Rect(x, y, barW, barH), Texture2D.whiteTexture);
            // Bar fill (gold)
            GUI.color = new Color(1f, 0.85f, 0.15f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, barW * frac, barH), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawTierHUD(float padding)
        {
            var diff = Core.DifficultyManager.Instance;
            if (diff == null) return;

            // Compact "TIER N" badge bottom-left, with a thin progress bar to the next tier.
            float w = 170f;
            float h = 36f;
            float x = padding;
            float y = Screen.height - h - padding;

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x - 4, y - 4, w + 8, h + 8), Texture2D.whiteTexture);

            var label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold
            };
            label.normal.textColor = new Color(0.9f, 0.95f, 1f);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8, y, w - 16, 22), $"TIER {diff.Tier + 1}  \u2022  {diff.CurrentDistance:0}m", label);

            // Tier progress bar
            float barY = y + 24f;
            GUI.color = new Color(0.15f, 0.18f, 0.25f, 0.9f);
            GUI.DrawTexture(new Rect(x + 8, barY, w - 16, 6), Texture2D.whiteTexture);
            GUI.color = new Color(0.4f, 0.7f, 1f, 0.95f);
            GUI.DrawTexture(new Rect(x + 8, barY, (w - 16) * diff.TierProgress, 6), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Tier-up celebration burst (centre-top)
            if (tierUpFlashTimer > 0f)
            {
                float t = Mathf.Clamp01(tierUpFlashTimer / 1.6f);
                float alpha = t;
                int oldFont = comboBigStyle.fontSize;
                comboBigStyle.fontSize = Mathf.RoundToInt(56f + (1f - t) * 24f);
                comboBigStyle.normal.textColor = new Color(0.4f, 0.85f, 1f, alpha);
                GUI.Label(new Rect(0, Screen.height * 0.32f, Screen.width, 100),
                    $"TIER UP  \u2192  {diff.Tier + 1}", comboBigStyle);
                comboBigStyle.fontSize = oldFont;
                comboBigStyle.normal.textColor = Color.white;
            }
        }

        private void InitStyles()
        {
            scoreStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            scoreStyle.normal.textColor = Color.white;

            warningStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperRight
            };
            warningStyle.normal.textColor = Color.red;

            infoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.UpperLeft
            };
            infoStyle.normal.textColor = new Color(1f, 1f, 1f, 0.8f);

            gameOverStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            gameOverStyle.normal.textColor = Color.red;

            comboStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            comboStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);

            comboBigStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 64,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            comboBigStyle.normal.textColor = Color.white;

            stylesInitialized = true;
        }

        private void Update()
        {
            // Pause toggle — Esc or Q
            bool pauseKey = false;
            if (Keyboard.current != null)
                pauseKey = Keyboard.current.escapeKey.wasPressedThisFrame
                        || Keyboard.current.qKey.wasPressedThisFrame;
            if (!pauseKey)
                pauseKey = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Q);

            if (pauseKey)
            {
                var gm = Core.GameManager.Instance;
                if (gm != null && gm.CurrentState == Core.GameManager.GameState.GameOver)
                    return;

                // Block pause while the death cinematic is playing — the cinematic
                // owns Time.timeScale (slow-motion) and unpause would clobber it back to 1.
                var ds = GetDeathSeq();
                if (ds != null && ds.IsPlaying)
                    return;

                if (showSettings)
                {
                    showSettings = false;
                    return;
                }

                if (showAchievements)
                {
                    showAchievements = false;
                    return;
                }

                isPaused = !isPaused;
                Time.timeScale = isPaused ? 0f : 1f;
            }

            // Stumble flash detection
            bool currentlyStumbling = rabbit != null && rabbit.IsStumbling;
            if (currentlyStumbling && !wasStumbling)
                stumbleFlashTimer = 0.5f;
            wasStumbling = currentlyStumbling;
            if (stumbleFlashTimer > 0f)
                stumbleFlashTimer -= Time.unscaledDeltaTime;
            if (multiplierFlashTimer > 0f)
                multiplierFlashTimer -= Time.unscaledDeltaTime;
            if (nearMissFlashTimer > 0f)
                nearMissFlashTimer -= Time.unscaledDeltaTime;
            if (tierUpFlashTimer > 0f)
                tierUpFlashTimer -= Time.unscaledDeltaTime;
        }

        private void OnGUI()
        {
            if (!stylesInitialized) InitStyles();

            // Hide HUD during death cinematic (except game over overlay after it ends)
            var deathSeqCheck = GetDeathSeq();
            bool deathPlaying = deathSeqCheck != null && deathSeqCheck.IsPlaying;

            float padding = 20f;
            var score = Core.ScoreManager.Instance;
            var game = Core.GameManager.Instance;

            // Skip HUD drawing during death sequence (cinematic plays full-screen)
            if (deathPlaying) return;

            // Score (top-left)
            string scoreText = $"🥕 {(score != null ? score.CurrentScore : 0)}";
            GUI.Label(new Rect(padding, padding, 300, 50), scoreText, scoreStyle);

            // Combo / streak indicator (under-score, only when active)
            DrawComboHUD(score, padding);
            DrawNearMissPop();
            DrawMagnetHUD(padding);
            DrawTierHUD(padding);
            // Speed (below score)
            if (rabbit != null)
            {
                string speedText = $"Speed: {rabbit.CurrentSpeed:F1}";
                GUI.Label(new Rect(padding, padding + 45, 300, 30), speedText, infoStyle);

                string laneText = $"Lane: {rabbit.CurrentLane + 1}/5";
                if (rabbit.IsJumping) laneText += "  🐇 JUMP!";
                GUI.Label(new Rect(padding, padding + 70, 300, 30), laneText, infoStyle);
            }

            // Farmer distance warning (top-right)
            if (farmer != null)
            {
                float threat = farmer.NormalizedThreat;
                string farmerText = threat > 0.7f ? "⚠️ FARMER CLOSE!" :
                                    threat > 0.4f ? "👨‍🌾 Farmer gaining..." :
                                                     "👨‍🌾 Farmer distant";
                warningStyle.normal.textColor = Color.Lerp(Color.yellow, Color.red, threat);
                GUI.Label(new Rect(Screen.width - 320, padding, 300, 50), farmerText, warningStyle);

                // Farmer distance bar
                float barWidth = 200f;
                float barHeight = 12f;
                float barX = Screen.width - padding - barWidth;
                float barY = padding + 40;

                GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.7f);
                GUI.DrawTexture(new Rect(barX, barY, barWidth, barHeight), Texture2D.whiteTexture);
                GUI.color = Color.Lerp(Color.green, Color.red, threat);
                GUI.DrawTexture(new Rect(barX, barY, barWidth * threat, barHeight), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // Stumble warning flash (red border)
            if (stumbleFlashTimer > 0f)
            {
                float alpha = Mathf.Clamp01(stumbleFlashTimer / 0.5f) * 0.4f;
                GUI.color = new Color(1f, 0f, 0f, alpha);
                float border = 15f;
                GUI.DrawTexture(new Rect(0, 0, border, Screen.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(Screen.width - border, 0, border, Screen.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, border), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, Screen.height - border, Screen.width, border), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // Stumble danger warning
            if (rabbit != null && rabbit.InDangerWindow)
            {
                var prevAlign = warningStyle.alignment;
                var prevColor = warningStyle.normal.textColor;
                warningStyle.alignment = TextAnchor.UpperCenter;
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                warningStyle.normal.textColor = new Color(1f, 0.2f, 0.2f, pulse);
                GUI.Label(new Rect(0, padding + 60, Screen.width, 40),
                    "\u26a0\ufe0f WATCH OUT \u2014 one more stumble!", warningStyle);
                warningStyle.alignment = prevAlign;
                warningStyle.normal.textColor = prevColor;
            }

            // High score
            if (score != null && score.HighScore > 0)
            {
                string highText = $"Best: {score.HighScore}";
                GUI.Label(new Rect(padding, padding + 95, 300, 30), highText, infoStyle);
            }

            // Daily Run ribbon — top centre, visible the whole run.
            if (Core.DailyRun.IsActive)
            {
                int dailyBest = Core.DailyRun.TodayBestScore;
                string ribbon = dailyBest > 0
                    ? $"📅 DAILY  {Core.DailyRun.TodayLabel}  •  seed #{Core.DailyRun.TodaySeed:X8}  •  today's best: {dailyBest}"
                    : $"📅 DAILY  {Core.DailyRun.TodayLabel}  •  seed #{Core.DailyRun.TodaySeed:X8}  •  set the bar!";
                var prevAlign = infoStyle.alignment;
                var prevColor = infoStyle.normal.textColor;
                infoStyle.alignment = TextAnchor.UpperCenter;
                infoStyle.normal.textColor = new Color(1f, 0.86f, 0.35f);
                GUI.Label(new Rect(0, 6f, Screen.width, 22f), ribbon, infoStyle);
                infoStyle.alignment = prevAlign;
                infoStyle.normal.textColor = prevColor;
            }

            // Tutorial prompt — top-centre, animated pulse so it stands out.
            if (Core.Tutorial.IsActive && Core.Tutorial.Current != Core.Tutorial.Step.Done)
            {
                string prompt = Core.Tutorial.CurrentPrompt;
                if (!string.IsNullOrEmpty(prompt))
                {
                    var prevAlign2 = infoStyle.alignment;
                    var prevColor2 = infoStyle.normal.textColor;
                    var prevSize = infoStyle.fontSize;
                    float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
                    infoStyle.alignment = TextAnchor.UpperCenter;
                    infoStyle.fontSize = 22;
                    infoStyle.normal.textColor = new Color(1f, 1f, 0.7f, pulse);

                    float bgY = 32f, bgH = 36f;
                    GUI.color = new Color(0f, 0f, 0f, 0.55f);
                    GUI.DrawTexture(new Rect(Screen.width * 0.15f, bgY, Screen.width * 0.7f, bgH), Texture2D.whiteTexture);
                    GUI.color = Color.white;

                    GUI.Label(new Rect(0, bgY + 6f, Screen.width, bgH), prompt, infoStyle);
                    infoStyle.alignment = prevAlign2;
                    infoStyle.normal.textColor = prevColor2;
                    infoStyle.fontSize = prevSize;
                }
            }

            // Chunk/Distance debug stats (bottom-left)
            var chunkMgr = GetChunkMgr();
            if (chunkMgr != null)
            {
                float y = Screen.height - 110;
                GUI.Label(new Rect(padding, y, 400, 25),
                    $"Distance: {chunkMgr.TotalDistance:F0}m", infoStyle);
                GUI.Label(new Rect(padding, y + 22, 400, 25),
                    $"Chunk: #{chunkMgr.CurrentChunkIndex}  (active: {chunkMgr.ActiveChunkCount})", infoStyle);
                GUI.Label(new Rect(padding, y + 44, 400, 25),
                    $"Origin shifts: {chunkMgr.OriginShiftCount}", infoStyle);
            }

            // Pause overlay
            if (isPaused)
            {
                GUI.color = new Color(0, 0, 0, 0.75f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;

                if (!showSettings && !showAchievements)
                {
                    gameOverStyle.normal.textColor = Color.white;
                    GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 60), "PAUSED", gameOverStyle);
                    gameOverStyle.normal.textColor = Color.red;

                    if (buttonStyle == null)
                    {
                        buttonStyle = new GUIStyle(GUI.skin.button)
                        {
                            fontSize = 28,
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.MiddleCenter
                        };
                        buttonStyle.normal.textColor = Color.white;
                    }

                    float btnW = 300, btnH = 50;
                    float btnX = (Screen.width - btnW) / 2f;

                    if (GUI.Button(new Rect(btnX, Screen.height * 0.36f, btnW, btnH), "▶  RESUME", buttonStyle))
                    {
                        Core.AudioManager.Instance?.PlayMenuClick();
                        isPaused = false;
                        Time.timeScale = 1f;
                    }

                    if (GUI.Button(new Rect(btnX, Screen.height * 0.45f, btnW, btnH), "⚙  SETTINGS", buttonStyle))
                    {
                        Core.AudioManager.Instance?.PlayMenuClick();
                        showSettings = true;
                    }

                    string achLabel = $"🏆  ACHIEVEMENTS  ({Core.Achievements.UnlockedCount}/{Core.Achievements.Total})";
                    if (GUI.Button(new Rect(btnX, Screen.height * 0.54f, btnW, btnH), achLabel, buttonStyle))
                    {
                        Core.AudioManager.Instance?.PlayMenuClick();
                        showAchievements = true;
                    }

                    if (GUI.Button(new Rect(btnX, Screen.height * 0.63f, btnW, btnH), "✕  QUIT TO MENU", buttonStyle))
                    {
                        Core.AudioManager.Instance?.PlayMenuClick();
                        isPaused = false;
                        Core.GameManager.Instance?.ReturnToMenu();
                    }

                    infoStyle.alignment = TextAnchor.MiddleCenter;
                    GUI.Label(new Rect(0, Screen.height * 0.74f, Screen.width, 30),
                        "Esc or Q to resume", infoStyle);
                    infoStyle.alignment = TextAnchor.UpperLeft;
                }
                else if (showSettings)
                {
                    // Settings sub-panel
                    gameOverStyle.normal.textColor = Color.white;
                    GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 60), "SETTINGS", gameOverStyle);
                    gameOverStyle.normal.textColor = Color.red;

                    float cx = Screen.width / 2f;

                    infoStyle.alignment = TextAnchor.MiddleCenter;
                    infoStyle.fontSize = 22;
                    GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 30), "CONTROLS", infoStyle);
                    infoStyle.fontSize = 18;
                    GUI.Label(new Rect(0, Screen.height * 0.40f, Screen.width, 60),
                        "PC: A/D or ←/→ = switch lanes | Space/W/↑ = jump | Numpad = mirrors | Esc/Q = pause", infoStyle);
                    GUI.Label(new Rect(0, Screen.height * 0.46f, Screen.width, 30),
                        "Mobile: Swipe lanes • Swipe up or tap to jump", infoStyle);

                    GUI.Label(new Rect(0, Screen.height * 0.55f, Screen.width, 30), "Master Volume", infoStyle);
                    float vol = GUI.HorizontalSlider(
                        new Rect(cx - 150, Screen.height * 0.60f, 300, 20),
                        AudioListener.volume, 0f, 1f);
                    if (Core.AudioManager.Instance != null)
                        Core.AudioManager.Instance.MasterVolume = vol;
                    else
                    {
                        AudioListener.volume = vol;
                        PlayerPrefs.SetFloat("MasterVolume", vol);
                    }

                    // SFX Volume
                    GUI.Label(new Rect(0, Screen.height * 0.63f, Screen.width, 30), "SFX Volume", infoStyle);
                    float sfxVol = Core.AudioManager.Instance != null ? Core.AudioManager.Instance.SFXVolume : 1f;
                    sfxVol = GUI.HorizontalSlider(
                        new Rect(cx - 150, Screen.height * 0.67f, 300, 20),
                        sfxVol, 0f, 1f);
                    if (Core.AudioManager.Instance != null)
                        Core.AudioManager.Instance.SFXVolume = sfxVol;

                    // Music Volume
                    GUI.Label(new Rect(0, Screen.height * 0.70f, Screen.width, 30), "Music Volume", infoStyle);
                    float musicVol = Core.MusicPlayer.Instance != null ? Core.MusicPlayer.Instance.Volume : 0.35f;
                    musicVol = GUI.HorizontalSlider(
                        new Rect(cx - 150, Screen.height * 0.74f, 300, 20),
                        musicVol, 0f, 1f);
                    if (Core.MusicPlayer.Instance != null)
                        Core.MusicPlayer.Instance.Volume = musicVol;

                    // Persist all volume changes once the user releases the slider /
                    // lifts a finger. Cheaper than calling PlayerPrefs.Save() every
                    // frame while dragging; lifecycle hooks still cover quit/pause.
                    if (Event.current != null && Event.current.type == EventType.MouseUp)
                    {
                        Core.AudioManager.Instance?.FlushVolumePrefs();
                        Core.MusicPlayer.Instance?.FlushVolumePrefs();
                        PlayerPrefs.Save();
                    }

                    // Death particle mode toggle
                    infoStyle.fontSize = 22;
                    GUI.Label(new Rect(0, Screen.height * 0.78f, Screen.width, 30), "Death Effect", infoStyle);
                    infoStyle.fontSize = 18;
                    bool useBlood = Core.DeathSequence.UseBloodParticles;
                    string modeLabel = useBlood ? "🩸 Blood (click to change)" : "🥕 Carrots (click to change)";
                    if (GUI.Button(new Rect(cx - 150, Screen.height * 0.825f, 300, 35), modeLabel, buttonStyle))
                    {
                        Core.AudioManager.Instance?.PlayMenuClick();
                        Core.DeathSequence.UseBloodParticles = !useBlood;
                    }

                    if (buttonStyle == null)
                    {
                        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };
                        buttonStyle.normal.textColor = Color.white;
                    }
                    float btnW = 200, btnH = 45;
                    if (GUI.Button(new Rect(cx - btnW / 2, Screen.height * 0.90f, btnW, btnH), "← BACK", buttonStyle))
                    {
                        Core.AudioManager.Instance?.PlayMenuClick();
                        showSettings = false;
                    }
                    infoStyle.alignment = TextAnchor.UpperLeft;
                }
                else // showAchievements
                {
                    DrawAchievementsPanel();
                }

                return; // Don't draw game over or controls hint while paused
            }

            // Game Over overlay — but NOT while death sequence is playing
            var deathSeq = GetDeathSeq();
            if (game != null && game.CurrentState == Core.GameManager.GameState.GameOver
                && (deathSeq == null || !deathSeq.IsPlaying))
            {
                // Dark overlay
                GUI.color = new Color(0, 0, 0, 0.72f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 60), "GAME OVER", gameOverStyle);

                int finalScore = score?.CurrentScore ?? 0;
                int best = score?.HighScore ?? 0;
                float runDist = score?.CurrentRunDistance ?? 0f;
                float bestDist = score?.BestDistance ?? 0f;
                int maxCombo = score?.MaxComboReached ?? 0;
                int maxMult = score?.MaxMultiplierReached ?? 1;
                int maxTier = score?.MaxTierReached ?? 0;
                bool newBestScore = score != null && score.LastRunWasNewBestScore;
                bool newBestDist = score != null && score.LastRunWasNewBestDistance;

                scoreStyle.alignment = TextAnchor.MiddleCenter;

                // Run score line
                int row = (int)(Screen.height * 0.32f);
                GUI.Label(new Rect(0, row, Screen.width, 50),
                    $"Score: {finalScore}", scoreStyle);
                if (newBestScore)
                {
                    var prev = scoreStyle.normal.textColor;
                    scoreStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);
                    GUI.Label(new Rect(0, row + 44, Screen.width, 28),
                        "★ NEW BEST! ★", scoreStyle);
                    scoreStyle.normal.textColor = prev;
                }

                // Best score
                infoStyle.alignment = TextAnchor.MiddleCenter;
                infoStyle.fontSize = 22;
                GUI.Label(new Rect(0, row + 78, Screen.width, 28),
                    $"Best: {best}", infoStyle);

                // Distance
                GUI.Label(new Rect(0, row + 112, Screen.width, 28),
                    $"Distance: {runDist:0} m   (best {bestDist:0} m)", infoStyle);
                if (newBestDist)
                {
                    var prev = infoStyle.normal.textColor;
                    infoStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);
                    GUI.Label(new Rect(0, row + 138, Screen.width, 26),
                        "★ FURTHEST RUN! ★", infoStyle);
                    infoStyle.normal.textColor = prev;
                }

                // Run highlights
                infoStyle.fontSize = 18;
                GUI.Label(new Rect(0, row + 172, Screen.width, 24),
                    $"Tier reached: {maxTier + 1}   |   Best combo: {maxCombo} (x{maxMult})", infoStyle);

                // Watch-replay button — only when a replay buffer is available.
                var rec = Core.DeathReplayRecorder.Instance;
                if (rec != null && rec.Count >= 4)
                {
                    if (buttonStyle == null)
                    {
                        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
                        buttonStyle.normal.textColor = Color.white;
                    }
                    float btnW = 260f, btnH = 42f;
                    if (GUI.Button(new Rect((Screen.width - btnW) * 0.5f, row + 208, btnW, btnH),
                                   replayActive ? "■  STOP REPLAY" : "▶  WATCH REPLAY", buttonStyle))
                    {
                        Core.AudioManager.Instance?.PlayMenuClick();
                        if (replayActive)
                        {
                            replayActive = false;
                        }
                        else
                        {
                            replayActive = true;
                            replayStartedAt = Time.unscaledTime;
                        }
                    }
                }

                if (replayActive) DrawDeathReplayPanel();

                infoStyle.fontSize = 24;
                GUI.Label(new Rect(0, Screen.height * 0.78f, Screen.width, 40),
                    "Press R to restart | M for menu", infoStyle);
                infoStyle.alignment = TextAnchor.UpperLeft;
                infoStyle.fontSize = 18;
                scoreStyle.alignment = TextAnchor.UpperLeft;

                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                {
                    Core.GameManager.Instance?.RestartGame();
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                }

                if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
                {
                    Time.timeScale = 1f;
                    UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
                }
            }

            // Controls hint (bottom)
            infoStyle.alignment = TextAnchor.LowerCenter;
            GUI.Label(new Rect(0, Screen.height - 50, Screen.width, 40),
                "A/D or ←/→ to switch lanes | Space/W/↑ to jump", infoStyle);
            infoStyle.alignment = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// Renders a top-down replay of the rabbit + farmer over the captured
        /// window. Auto-fits the trail to the panel; advances a play-head over
        /// <see cref="ReplayPlaybackDuration"/> seconds of unscaled time, then
        /// holds on the final frame so the player can read it.
        /// </summary>
        private void DrawDeathReplayPanel()
        {
            var rec = Core.DeathReplayRecorder.Instance;
            if (rec == null || rec.Count < 2) return;

            float panelW = Mathf.Min(420f, Screen.width - 80f);
            float panelH = panelW * 0.6f;
            float panelX = (Screen.width - panelW) * 0.5f;
            float panelY = Screen.height * 0.55f;

            // Background
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.84f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
            };
            titleStyle.normal.textColor = new Color(1f, 0.86f, 0.4f);
            GUI.Label(new Rect(panelX, panelY + 4f, panelW, 22f), "LAST 3 SECONDS — TOP DOWN", titleStyle);

            // Bounds in world space (auto-fit).
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
            int n = rec.Count;
            for (int i = 0; i < n; i++)
            {
                var s = rec.GetSnapshot(i);
                if (s.Rabbit.x < minX) minX = s.Rabbit.x;
                if (s.Rabbit.x > maxX) maxX = s.Rabbit.x;
                if (s.Rabbit.z < minZ) minZ = s.Rabbit.z;
                if (s.Rabbit.z > maxZ) maxZ = s.Rabbit.z;
                if (s.HasFarmer)
                {
                    if (s.Farmer.x < minX) minX = s.Farmer.x;
                    if (s.Farmer.x > maxX) maxX = s.Farmer.x;
                    if (s.Farmer.z < minZ) minZ = s.Farmer.z;
                    if (s.Farmer.z > maxZ) maxZ = s.Farmer.z;
                }
            }
            // Pad
            float pad = 1.5f;
            minX -= pad; maxX += pad; minZ -= pad; maxZ += pad;
            float spanX = Mathf.Max(0.001f, maxX - minX);
            float spanZ = Mathf.Max(0.001f, maxZ - minZ);

            // Inner area inside the panel (leave room for title).
            float innerX = panelX + 12f;
            float innerY = panelY + 28f;
            float innerW = panelW - 24f;
            float innerH = panelH - 40f;

            // Use min of axes so aspect is preserved.
            float scale = Mathf.Min(innerW / spanX, innerH / spanZ);
            float drawW = spanX * scale;
            float drawH = spanZ * scale;
            float ox = innerX + (innerW - drawW) * 0.5f;
            float oy = innerY + (innerH - drawH) * 0.5f;

            // Map a world point to panel coordinates. Z runs into screen so we
            // flip it so 'forward' shows as up on the panel.
            Vector2 ToPanel(Vector3 w)
                => new Vector2(ox + (w.x - minX) * scale,
                               oy + drawH - (w.z - minZ) * scale);

            // Play-head
            float age = Time.unscaledTime - replayStartedAt;
            float t = Mathf.Clamp01(age / ReplayPlaybackDuration);
            int headIndex = Mathf.Clamp(Mathf.FloorToInt(t * (n - 1)), 0, n - 1);

            // Draw trails up to play-head: rabbit (white) and farmer (red).
            for (int i = 1; i <= headIndex; i++)
            {
                var a = rec.GetSnapshot(i - 1);
                var b = rec.GetSnapshot(i);
                DrawPanelLine(ToPanel(a.Rabbit), ToPanel(b.Rabbit), new Color(1f, 1f, 1f, 0.9f), 2f);
                if (a.HasFarmer && b.HasFarmer)
                    DrawPanelLine(ToPanel(a.Farmer), ToPanel(b.Farmer), new Color(1f, 0.3f, 0.3f, 0.9f), 2f);
            }

            // Heads
            var head = rec.GetSnapshot(headIndex);
            DrawPanelDot(ToPanel(head.Rabbit), 5f, new Color(1f, 0.92f, 0.35f));
            if (head.HasFarmer)
                DrawPanelDot(ToPanel(head.Farmer), 5f, new Color(1f, 0.2f, 0.2f));

            // Legend / scrub bar
            var legend = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            legend.normal.textColor = new Color(1f, 0.92f, 0.35f);
            GUI.Label(new Rect(panelX + 10f, panelY + panelH - 18f, 80f, 16f), "● Rabbit", legend);
            legend.normal.textColor = new Color(1f, 0.3f, 0.3f);
            GUI.Label(new Rect(panelX + 90f, panelY + panelH - 18f, 80f, 16f), "● Farmer", legend);

            // Progress bar
            float barX = panelX + panelW - 110f;
            float barY = panelY + panelH - 14f;
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            GUI.DrawTexture(new Rect(barX, barY, 100f, 4f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.86f, 0.4f, 0.95f);
            GUI.DrawTexture(new Rect(barX, barY, 100f * t, 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static void DrawPanelDot(Vector2 p, float r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(p.x - r, p.y - r, r * 2f, r * 2f), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static void DrawPanelLine(Vector2 a, Vector2 b, Color c, float thickness)
        {
            // Cheap 2D line via rotated stretched 1×1 white texture.
            var prev = GUI.color;
            GUI.color = c;
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) { GUI.color = prev; return; }
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var pivot = new Vector2(a.x, a.y);
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, pivot);
            GUI.DrawTexture(new Rect(a.x, a.y - thickness * 0.5f, len, thickness), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = prev;
        }

        /// <summary>Renders the achievements list inside the pause overlay.</summary>
        private void DrawAchievementsPanel()
        {
            gameOverStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(0, Screen.height * 0.10f, Screen.width, 60),
                $"ACHIEVEMENTS  {Core.Achievements.UnlockedCount}/{Core.Achievements.Total}",
                gameOverStyle);
            gameOverStyle.normal.textColor = Color.red;

            float listW = Mathf.Min(640f, Screen.width - 80f);
            float listX = (Screen.width - listW) * 0.5f;
            float listY = Screen.height * 0.22f;
            float listH = Screen.height * 0.62f;

            var defs = Core.Achievements.All;
            float rowH = 70f;
            float contentH = defs.Count * (rowH + 6f);

            var view = new Rect(listX, listY, listW, listH);
            var content = new Rect(0, 0, listW - 24f, contentH);
            achievementsScroll = GUI.BeginScrollView(view, achievementsScroll, content);

            var iconStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32, alignment = TextAnchor.MiddleCenter,
            };
            var titleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
            };
            var descLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, alignment = TextAnchor.MiddleLeft, wordWrap = true,
            };

            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                bool unlocked = Core.Achievements.IsUnlocked(def.Id);
                float y = i * (rowH + 6f);

                GUI.color = unlocked ? new Color(0.18f, 0.32f, 0.18f, 0.9f)
                                     : new Color(0.18f, 0.18f, 0.18f, 0.85f);
                GUI.DrawTexture(new Rect(0, y, content.width, rowH), Texture2D.whiteTexture);
                GUI.color = unlocked ? new Color(1f, 0.84f, 0.2f, 1f)
                                     : new Color(0.4f, 0.4f, 0.4f, 1f);
                GUI.DrawTexture(new Rect(0, y, 4f, rowH), Texture2D.whiteTexture);
                GUI.color = Color.white;

                titleLabel.normal.textColor = unlocked ? Color.white : new Color(0.65f, 0.65f, 0.65f);
                descLabel.normal.textColor  = unlocked ? new Color(0.85f, 0.85f, 0.85f)
                                                       : new Color(0.55f, 0.55f, 0.55f);
                iconStyle.normal.textColor = unlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f);

                GUI.Label(new Rect(8f, y, 60f, rowH), unlocked ? def.Icon : "🔒", iconStyle);
                GUI.Label(new Rect(76f, y + 8f, content.width - 80f, 24f), def.Title, titleLabel);
                GUI.Label(new Rect(76f, y + 32f, content.width - 80f, 32f), def.Description, descLabel);
            }

            GUI.EndScrollView();

            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };
                buttonStyle.normal.textColor = Color.white;
            }
            float backW = 200f, backH = 45f;
            if (GUI.Button(new Rect((Screen.width - backW) * 0.5f, Screen.height * 0.90f, backW, backH),
                "← BACK", buttonStyle))
            {
                Core.AudioManager.Instance?.PlayMenuClick();
                showAchievements = false;
            }
        }
    }
}
