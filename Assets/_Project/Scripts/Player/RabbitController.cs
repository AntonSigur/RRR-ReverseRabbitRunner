using UnityEngine;
using UnityEngine.InputSystem;

namespace ReverseRabbitRunner.Player
{
    /// <summary>
    /// Controls the rabbit's lane-switching movement.
    /// The rabbit runs backwards automatically; player controls lateral movement.
    /// Uses the new Input System for cross-platform support.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class RabbitController : MonoBehaviour
    {
        [Header("Lane Settings")]
        [SerializeField] private float laneWidth = 3f;
        [SerializeField] private int laneCount = 5;
        [SerializeField] private float laneSwitchSpeed = 12f;

        [Header("Movement")]
        [SerializeField] private float forwardSpeed = 10f;
        [SerializeField] private float speedIncreaseRate = 0.05f;
        [SerializeField] private float maxSpeed = 30f;

        [Header("Physics")]
        [SerializeField] private float gravity = -30f;

        private CharacterController controller;
        private int currentLane;
        private float targetXPosition;
        private float verticalVelocity;
        private bool isAlive = true;

        // Input
        private InputAction moveAction;
        private bool lastFrameLeft;
        private bool lastFrameRight;

        public float CurrentSpeed => forwardSpeed;
        public bool IsAlive => isAlive;
        public int CurrentLane => currentLane;

        public event System.Action OnHitObstacle;
        public event System.Action<GameObject> OnCollectCarrot;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            currentLane = laneCount / 2;
            UpdateTargetPosition();
        }

        private void OnEnable()
        {
            // Create a simple move action with keyboard and gamepad bindings
            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a")
                .With("Positive", "<Keyboard>/d");
            moveAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow")
                .With("Positive", "<Keyboard>/rightArrow");
            moveAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Gamepad>/leftStick/left")
                .With("Positive", "<Gamepad>/leftStick/right");
            moveAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Gamepad>/dpad/left")
                .With("Positive", "<Gamepad>/dpad/right");

            moveAction.Enable();
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            moveAction?.Dispose();
        }

        private void Update()
        {
            if (!isAlive) return;

            HandleInput();

            // Gradually increase speed
            forwardSpeed = Mathf.Min(forwardSpeed + speedIncreaseRate * Time.deltaTime, maxSpeed);

            Vector3 movement = Vector3.zero;

            // Forward movement (rabbit runs backwards, so negative Z)
            movement.z = -forwardSpeed * Time.deltaTime;

            // Smooth lateral movement to target lane
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

        private void HandleInput()
        {
            float moveValue = moveAction.ReadValue<float>();

            // Detect "just pressed" by tracking previous frame state
            bool isLeft = moveValue < -0.5f;
            bool isRight = moveValue > 0.5f;

            if (isLeft && !lastFrameLeft)
                MoveLeft();
            if (isRight && !lastFrameRight)
                MoveRight();

            lastFrameLeft = isLeft;
            lastFrameRight = isRight;

            // Touch swipe (using Touchscreen from Input System)
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                // Store start position handled via pointer
            }
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
