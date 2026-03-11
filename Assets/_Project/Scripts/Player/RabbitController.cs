using UnityEngine;
using UnityEngine.InputSystem;

namespace ReverseRabbitRunner.Player
{
    /// <summary>
    /// Controls the rabbit's lane-switching movement.
    /// The rabbit runs backwards automatically; player controls lateral movement.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class RabbitController : MonoBehaviour
    {
        [Header("Lane Settings")]
        [SerializeField] private float laneWidth = 3f;
        [SerializeField] private int laneCount = 3;
        [SerializeField] private float laneSwitchSpeed = 10f;

        [Header("Movement")]
        [SerializeField] private float forwardSpeed = 10f;
        [SerializeField] private float speedIncreaseRate = 0.1f;
        [SerializeField] private float maxSpeed = 30f;

        [Header("Physics")]
        [SerializeField] private float gravity = -30f;

        private CharacterController controller;
        private int currentLane; // 0 = left, 1 = center, 2 = right
        private float targetXPosition;
        private float verticalVelocity;
        private bool isAlive = true;

        public float CurrentSpeed => forwardSpeed;
        public bool IsAlive => isAlive;

        public event System.Action OnHitObstacle;
        public event System.Action<GameObject> OnCollectCarrot;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            currentLane = laneCount / 2; // Start in center lane
            UpdateTargetPosition();
        }

        private void Update()
        {
            if (!isAlive) return;

            // Gradually increase speed
            forwardSpeed = Mathf.Min(forwardSpeed + speedIncreaseRate * Time.deltaTime, maxSpeed);

            // Calculate movement
            Vector3 movement = Vector3.zero;

            // Forward movement (rabbit runs backwards, so negative Z is "forward" for the rabbit)
            movement.z = -forwardSpeed * Time.deltaTime;

            // Lateral lane switching (smooth lerp to target)
            float currentX = transform.position.x;
            float newX = Mathf.Lerp(currentX, targetXPosition, laneSwitchSpeed * Time.deltaTime);
            movement.x = newX - currentX;

            // Gravity
            if (controller.isGrounded)
                verticalVelocity = -1f;
            else
                verticalVelocity += gravity * Time.deltaTime;

            movement.y = verticalVelocity * Time.deltaTime;

            controller.Move(movement);
        }

        public void MoveLeft()
        {
            if (currentLane > 0)
            {
                currentLane--;
                UpdateTargetPosition();
            }
        }

        public void MoveRight()
        {
            if (currentLane < laneCount - 1)
            {
                currentLane++;
                UpdateTargetPosition();
            }
        }

        /// <summary>
        /// Called by InputSystem via PlayerInput component or direct binding.
        /// </summary>
        public void OnMove(InputAction.CallbackContext context)
        {
            if (!context.performed) return;

            float direction = context.ReadValue<float>();
            if (direction < 0) MoveLeft();
            else if (direction > 0) MoveRight();
        }

        public void Die()
        {
            isAlive = false;
            Core.GameManager.Instance?.GameOver();
        }

        public void HitObstacle()
        {
            OnHitObstacle?.Invoke();
        }

        private void UpdateTargetPosition()
        {
            int centerLane = laneCount / 2;
            targetXPosition = (currentLane - centerLane) * laneWidth;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Carrot"))
            {
                OnCollectCarrot?.Invoke(other.gameObject);
                Core.ScoreManager.Instance?.AddScore(1);
                Destroy(other.gameObject);
            }
            else if (other.CompareTag("Obstacle"))
            {
                HitObstacle();
            }
            else if (other.CompareTag("PowerUp"))
            {
                // PowerUp handling delegated to individual PowerUp scripts
            }
        }
    }
}
