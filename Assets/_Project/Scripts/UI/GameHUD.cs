using UnityEngine;
using UnityEngine.InputSystem;

namespace ReverseRabbitRunner.UI
{
    /// <summary>
    /// Simple in-game HUD showing score, speed, and farmer distance.
    /// Uses OnGUI for quick prototyping — will be replaced with proper UI Canvas later.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Player.RabbitController rabbit;
        [SerializeField] private Enemies.FarmerController farmer;

        private GUIStyle scoreStyle;
        private GUIStyle warningStyle;
        private GUIStyle infoStyle;
        private GUIStyle gameOverStyle;
        private bool stylesInitialized = false;

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

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!stylesInitialized) InitStyles();

            float padding = 20f;
            var score = Core.ScoreManager.Instance;
            var game = Core.GameManager.Instance;

            // Score (top-left)
            string scoreText = $"🥕 {(score != null ? score.CurrentScore : 0)}";
            GUI.Label(new Rect(padding, padding, 300, 50), scoreText, scoreStyle);

            // Speed (below score)
            if (rabbit != null)
            {
                string speedText = $"Speed: {rabbit.CurrentSpeed:F1}";
                GUI.Label(new Rect(padding, padding + 45, 300, 30), speedText, infoStyle);

                string laneText = $"Lane: {rabbit.CurrentLane + 1}/5";
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

            // High score
            if (score != null && score.HighScore > 0)
            {
                string highText = $"Best: {score.HighScore}";
                GUI.Label(new Rect(padding, padding + 95, 300, 30), highText, infoStyle);
            }

            // Chunk/Distance debug stats (bottom-left)
            var chunkMgr = FindAnyObjectByType<World.ChunkManager>();
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

            // Game Over overlay
            if (game != null && game.CurrentState == Core.GameManager.GameState.GameOver)
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
                    "Press R to restart", infoStyle);
                infoStyle.alignment = TextAnchor.UpperLeft;
                infoStyle.fontSize = 18;

                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                {
                    Core.GameManager.Instance?.RestartGame();
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                }
            }

            // Controls hint (bottom)
            infoStyle.alignment = TextAnchor.LowerCenter;
            GUI.Label(new Rect(0, Screen.height - 50, Screen.width, 40),
                "A/D or ←/→ to switch lanes", infoStyle);
            infoStyle.alignment = TextAnchor.UpperLeft;
        }
    }
}
