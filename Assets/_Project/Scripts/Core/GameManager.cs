using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Central game state manager — controls game flow, score, and lifecycle.
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
            DontDestroyOnLoad(gameObject);
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

        private void SetState(GameState newState)
        {
            currentState = newState;
            OnGameStateChanged?.Invoke(newState);
        }
    }
}
