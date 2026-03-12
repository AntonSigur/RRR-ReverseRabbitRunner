using UnityEngine;

namespace ReverseRabbitRunner.Enemies
{
    /// <summary>
    /// Controls the angry farmer who chases the rabbit.
    /// The farmer is visible in front of the rabbit (since the rabbit runs backwards).
    /// Gets closer when the rabbit hits obstacles.
    /// Has natural, slightly delayed movement — doesn't perfectly mirror the rabbit.
    /// </summary>
    public class FarmerController : MonoBehaviour
    {
        [Header("Chase Settings")]
        [SerializeField] private float baseDistance = 5f;
        [SerializeField] private float catchUpSpeed = 2f;
        [SerializeField] private float fallBackSpeed = 1f;
        [SerializeField] private float catchDistance = 1f;

        [Header("Obstacle Penalty")]
        [SerializeField] private float obstaclePenaltyDistance = 3f;
        [SerializeField] private float smallObstaclePenaltyDistance = 2f;
        [SerializeField] private float tallObstaclePenaltyDistance = 4f;
        [SerializeField] private float farmerRecoveryTime = 10f;

        [Header("Catch Animation")]
        [SerializeField] private float catchPauseDuration = 1.0f;

        [Header("Lateral Following")]
        [SerializeField] private float lateralFollowSpeed = 3f;
        [SerializeField] private float lateralRandomness = 0.5f;
        [SerializeField] private float swayAmount = 0.3f;
        [SerializeField] private float swaySpeed = 2f;

        [Header("References")]
        [SerializeField] private Transform playerTransform;

        private float currentDistance;
        private float currentX;
        private float swayOffset;
        private bool isCatching;
        private float catchTimer;
        private Player.RabbitController rabbitController;

        public float CurrentDistance => currentDistance;
        public float NormalizedThreat => 1f - Mathf.Clamp01(currentDistance / baseDistance);

        public event System.Action OnCaughtRabbit;

        private void Start()
        {
            currentDistance = baseDistance;

            if (playerTransform == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) playerTransform = playerObj.transform;
            }

            if (playerTransform != null)
            {
                currentX = playerTransform.position.x;
                rabbitController = playerTransform.GetComponent<Player.RabbitController>();
                if (rabbitController != null)
                {
                    rabbitController.OnStumble += OnRabbitStumble;
                }
            }
        }

        private void Update()
        {
            if (playerTransform == null) return;
            if (Core.GameManager.Instance?.CurrentState != Core.GameManager.GameState.Playing) return;

            // Catch animation: farmer pauses then kills
            if (isCatching)
            {
                catchTimer -= Time.deltaTime;
                Vector3 catchPos = new Vector3(
                    playerTransform.position.x,
                    0f,
                    playerTransform.position.z + catchDistance
                );
                transform.position = catchPos;
                Vector3 catchLook = playerTransform.position - transform.position;
                catchLook.y = 0;
                if (catchLook.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(catchLook);
                if (catchTimer <= 0f)
                {
                    OnCaughtRabbit?.Invoke();
                    rabbitController?.Die();
                }
                return;
            }

            // Farmer slowly falls back over time (rabbit outrunning)
            float recoveryRate = (baseDistance - catchDistance) / Mathf.Max(farmerRecoveryTime, 0.1f);
            currentDistance = Mathf.Min(currentDistance + recoveryRate * Time.deltaTime, baseDistance);

            // Lateral following — delayed and slightly off
            float targetX = playerTransform.position.x;
            currentX = Mathf.Lerp(currentX, targetX, lateralFollowSpeed * Time.deltaTime);

            // Add a natural sway so the farmer doesn't look robotic
            swayOffset = Mathf.Sin(Time.time * swaySpeed) * swayAmount;

            // Position the farmer
            Vector3 farmerPos = new Vector3(
                currentX + swayOffset,
                0f,
                playerTransform.position.z + currentDistance
            );
            transform.position = farmerPos;

            // Face towards the rabbit
            Vector3 lookDir = playerTransform.position - transform.position;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(lookDir);

            // Check if farmer caught the rabbit
            if (currentDistance <= catchDistance)
            {
                StartCatchSequence();
            }
        }

        public void OnRabbitStumble(float severity)
        {
            float penalty = severity > 3f ? tallObstaclePenaltyDistance : smallObstaclePenaltyDistance;
            currentDistance = Mathf.Max(catchDistance, currentDistance - penalty);
        }

        private void StartCatchSequence()
        {
            if (isCatching) return;
            isCatching = true;
            catchTimer = catchPauseDuration;
        }

        private void OnDestroy()
        {
            if (rabbitController != null)
            {
                rabbitController.OnStumble -= OnRabbitStumble;
            }
        }
    }
}
