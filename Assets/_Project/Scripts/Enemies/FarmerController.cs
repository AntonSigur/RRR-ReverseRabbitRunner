using UnityEngine;

namespace ReverseRabbitRunner.Enemies
{
    /// <summary>
    /// Controls the angry farmer who chases the rabbit.
    /// The farmer is visible in front of the rabbit (since the rabbit runs backwards).
    /// Gets closer when the rabbit hits obstacles.
    /// </summary>
    public class FarmerController : MonoBehaviour
    {
        [Header("Chase Settings")]
        [SerializeField] private float baseDistance = 15f;
        [SerializeField] private float catchUpSpeed = 2f;
        [SerializeField] private float fallBackSpeed = 1f;
        [SerializeField] private float catchDistance = 1f;

        [Header("Obstacle Penalty")]
        [SerializeField] private float obstaclePenaltyDistance = 3f;

        [Header("References")]
        [SerializeField] private Transform playerTransform;

        private float currentDistance;
        private Player.RabbitController rabbitController;

        public float CurrentDistance => currentDistance;
        public float NormalizedThreat => 1f - (currentDistance / baseDistance);

        public event System.Action OnCaughtRabbit;

        private void Start()
        {
            currentDistance = baseDistance;

            if (playerTransform != null)
            {
                rabbitController = playerTransform.GetComponent<Player.RabbitController>();
                if (rabbitController != null)
                {
                    rabbitController.OnHitObstacle += OnRabbitHitObstacle;
                }
            }
        }

        private void Update()
        {
            if (playerTransform == null) return;
            if (Core.GameManager.Instance?.CurrentState != Core.GameManager.GameState.Playing) return;

            // Farmer slowly falls back over time (rabbit outrunning)
            currentDistance = Mathf.Min(currentDistance + fallBackSpeed * Time.deltaTime, baseDistance);

            // Position the farmer relative to the rabbit (in front, since rabbit runs backwards)
            Vector3 farmerPos = playerTransform.position;
            farmerPos.z += currentDistance;
            transform.position = farmerPos;

            // Check if farmer caught the rabbit
            if (currentDistance <= catchDistance)
            {
                CatchRabbit();
            }
        }

        private void OnRabbitHitObstacle()
        {
            // Farmer gets closer when rabbit hits obstacle
            currentDistance = Mathf.Max(catchDistance, currentDistance - obstaclePenaltyDistance);
        }

        private void CatchRabbit()
        {
            OnCaughtRabbit?.Invoke();
            rabbitController?.Die();
        }

        private void OnDestroy()
        {
            if (rabbitController != null)
            {
                rabbitController.OnHitObstacle -= OnRabbitHitObstacle;
            }
        }
    }
}
