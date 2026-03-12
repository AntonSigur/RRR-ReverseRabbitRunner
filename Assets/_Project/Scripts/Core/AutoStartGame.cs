using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Auto-starts the game when Play is pressed — only in the game scene.
    /// </summary>
    public class AutoStartGame : MonoBehaviour
    {
        [SerializeField] private float startDelay = 0.5f;

        private void Start()
        {
            // Only auto-start in the game scene, not in MainMenu
            if (SceneManager.GetActiveScene().name == "MainMenu")
                return;

            Invoke(nameof(StartGame), startDelay);
        }

        private void StartGame()
        {
            GameManager.Instance?.StartGame();
        }
    }
}
