using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Auto-starts the game when Play is pressed in the editor.
    /// Gives a brief countdown then starts gameplay.
    /// </summary>
    public class AutoStartGame : MonoBehaviour
    {
        [SerializeField] private float startDelay = 0.5f;

        private void Start()
        {
            Invoke(nameof(StartGame), startDelay);
        }

        private void StartGame()
        {
            GameManager.Instance?.StartGame();
        }
    }
}
