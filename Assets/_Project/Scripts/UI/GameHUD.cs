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

        private GUIStyle scoreStyle;
        private GUIStyle warningStyle;
        private GUIStyle infoStyle;
        private GUIStyle gameOverStyle;
        private GUIStyle buttonStyle;
        private bool stylesInitialized = false;
        private bool isPaused = false;
        private bool showSettings = false;
        private bool wasStumbling;
        private float stumbleFlashTimer;

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
        }

        private void OnDestroy()
        {
            if (Core.ScoreManager.Instance != null)
                Core.ScoreManager.Instance.OnComboChanged -= OnComboChanged;
        }

        private void OnComboChanged(int comboCount, int multiplier, bool tierIncreased)
        {
            if (tierIncreased)
                multiplierFlashTimer = 1.2f;
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

                if (!showSettings)
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

                    if (GUI.Button(new Rect(btnX, Screen.height * 0.38f, btnW, btnH), "▶  RESUME", buttonStyle))
                    {
                        Core.AudioManager.Instance?.PlayMenuClick();
                        isPaused = false;
                        Time.timeScale = 1f;
                    }

                    if (GUI.Button(new Rect(btnX, Screen.height * 0.48f, btnW, btnH), "⚙  SETTINGS", buttonStyle))
                    {
                        Core.AudioManager.Instance?.PlayMenuClick();
                        showSettings = true;
                    }

                    if (GUI.Button(new Rect(btnX, Screen.height * 0.58f, btnW, btnH), "✕  QUIT TO MENU", buttonStyle))
                    {
                        Core.AudioManager.Instance?.PlayMenuClick();
                        isPaused = false;
                        Core.GameManager.Instance?.ReturnToMenu();
                    }

                    infoStyle.alignment = TextAnchor.MiddleCenter;
                    GUI.Label(new Rect(0, Screen.height * 0.72f, Screen.width, 30),
                        "Esc or Q to resume", infoStyle);
                    infoStyle.alignment = TextAnchor.UpperLeft;
                }
                else
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
                        "Mobile: Swipe left/right to switch lanes", infoStyle);

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

                return; // Don't draw game over or controls hint while paused
            }

            // Game Over overlay — but NOT while death sequence is playing
            var deathSeq = GetDeathSeq();
            if (game != null && game.CurrentState == Core.GameManager.GameState.GameOver
                && (deathSeq == null || !deathSeq.IsPlaying))
            {
                // Dark overlay
                GUI.color = new Color(0, 0, 0, 0.6f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 60), "GAME OVER", gameOverStyle);

                scoreStyle.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(0, Screen.height * 0.45f, Screen.width, 50),
                    $"Carrots: {score?.CurrentScore ?? 0}", scoreStyle);
                scoreStyle.alignment = TextAnchor.UpperLeft;

                infoStyle.alignment = TextAnchor.MiddleCenter;
                infoStyle.fontSize = 24;
                GUI.Label(new Rect(0, Screen.height * 0.55f, Screen.width, 40),
                    "Press R to restart | M for menu", infoStyle);
                infoStyle.alignment = TextAnchor.UpperLeft;
                infoStyle.fontSize = 18;

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
    }
}
