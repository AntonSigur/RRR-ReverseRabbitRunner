using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Central game state manager — controls game flow, score, and lifecycle.
    /// Listens for scene changes to auto-reset state when returning to MainMenu.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState { Menu, Playing, Paused, GameOver }

        [Header("Game State")]
        [SerializeField] private GameState currentState = GameState.Menu;

        public GameState CurrentState => currentState;

        public event System.Action<GameState> OnGameStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Auto-reset to Menu state when MainMenu scene loads
            if (scene.name == "MainMenu")
            {
                Time.timeScale = 1f;
                SetState(GameState.Menu);
            }
        }

        public void StartGame()
        {
            SetState(GameState.Playing);
        }

        public void PauseGame()
        {
            if (currentState == GameState.Playing)
            {
                Time.timeScale = 0f;
                SetState(GameState.Paused);
            }
        }

        public void ResumeGame()
        {
            if (currentState == GameState.Paused)
            {
                Time.timeScale = 1f;
                SetState(GameState.Playing);
            }
        }

        public void GameOver()
        {
            Time.timeScale = 0f;
            SetState(GameState.GameOver);
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            ScoreManager.Instance?.ResetScore();
            SetState(GameState.Playing);
        }

        /// <summary>
        /// Properly return to the main menu — resets everything.
        /// </summary>
        public void ReturnToMenu()
        {
            Time.timeScale = 1f;
            ScoreManager.Instance?.ResetScore();
            SceneManager.LoadScene("MainMenu");
        }

        private void SetState(GameState newState)
        {
            currentState = newState;
            OnGameStateChanged?.Invoke(newState);
        }
    }
}
