using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Tracks score from carrot collection and manages high score persistence.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        private int currentScore;
        private int highScore;
        private int carrotsCollected;

        private const string HighScoreKey = "HighScore";

        public int CurrentScore => currentScore;
        public int HighScore => highScore;
        public int CarrotsCollected => carrotsCollected;

        public event System.Action<int> OnScoreChanged;
        public event System.Action<int> OnHighScoreBeaten;

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

            highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        }

        public void AddScore(int points)
        {
            currentScore += points;
            carrotsCollected++;
            OnScoreChanged?.Invoke(currentScore);

            if (currentScore > highScore)
            {
                highScore = currentScore;
                PlayerPrefs.SetInt(HighScoreKey, highScore);
                PlayerPrefs.Save();
                OnHighScoreBeaten?.Invoke(highScore);
            }
        }

        public void ResetScore()
        {
            currentScore = 0;
            carrotsCollected = 0;
            OnScoreChanged?.Invoke(currentScore);
        }
    }
}
