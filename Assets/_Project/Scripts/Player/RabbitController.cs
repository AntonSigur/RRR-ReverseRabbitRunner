using UnityEngine;
using UnityEngine.InputSystem;

namespace ReverseRabbitRunner.Player
{
    /// <summary>
    /// Controls the rabbit's lane-switching movement and jumping.
    /// The rabbit runs backwards automatically; player controls lateral movement and jumping.
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

        [Header("Jump Settings")]
        [SerializeField] private float jumpForce = 12f;
        [SerializeField] private float jumpSpeedPenalty = 1.5f;
        [SerializeField] private float jumpSpeedRecoveryRate = 2f;
        [SerializeField] private float airLaneSwitchMultiplier = 0.4f;
        [SerializeField] private float maxBodyTiltAngle = 15f;
        [SerializeField] private float bodyTiltSpeed = 8f;

        [Header("Physics")]
        [SerializeField] private float gravity = -30f;

        private CharacterController controller;
        private int currentLane;
        private float targetXPosition;
        private float verticalVelocity;
        private bool isAlive = true;
        private float baseSpeed;
        private float speedDebt;
        private bool isJumping;
        private float currentBodyTilt;
        private Transform bodyTransform;

        // Input
        private InputAction moveAction;
        private InputAction jumpAction;
        private bool lastFrameLeft;
        private bool lastFrameRight;

        public float CurrentSpeed => forwardSpeed;
        public bool IsAlive => isAlive;
        public int CurrentLane => currentLane;
        public bool IsGrounded => controller != null && controller.isGrounded;
        public bool IsJumping => isJumping;

        public event System.Action OnHitObstacle;
        public event System.Action<GameObject> OnCollectCarrot;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            currentLane = laneCount / 2;
            baseSpeed = forwardSpeed;
            UpdateTargetPosition();

            // Find the body child for tilt animation (first child with renderer)
            if (transform.childCount > 0)
                bodyTransform = transform.GetChild(0);
        }

        private void OnEnable()
        {
            // Lane switching input
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

            // Jump input
            jumpAction = new InputAction("Jump", InputActionType.Button);
            jumpAction.AddBinding("<Keyboard>/space");
            jumpAction.AddBinding("<Keyboard>/w");
            jumpAction.AddBinding("<Keyboard>/upArrow");
            jumpAction.AddBinding("<Gamepad>/buttonSouth");
            jumpAction.Enable();
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            moveAction?.Dispose();
            jumpAction?.Disable();
            jumpAction?.Dispose();
        }

        private void Update()
        {
            if (!isAlive) return;

            HandleInput();

            // Gradually increase speed (recover toward natural speed)
            baseSpeed = Mathf.Min(baseSpeed + speedIncreaseRate * Time.deltaTime, maxSpeed);

            // Recover from jump speed debt
            if (speedDebt > 0f)
            {
                float recovery = jumpSpeedRecoveryRate * Time.deltaTime;
                speedDebt = Mathf.Max(speedDebt - recovery, 0f);
            }

            forwardSpeed = Mathf.Max(baseSpeed - speedDebt, 2f);

            Vector3 movement = Vector3.zero;

            // Forward movement (rabbit runs backwards, so negative Z)
            movement.z = -forwardSpeed * Time.deltaTime;

            // Smooth lateral movement — slower when airborne
            float switchSpeed = laneSwitchSpeed;
            if (!controller.isGrounded)
                switchSpeed *= airLaneSwitchMultiplier;

            float currentX = transform.position.x;
            float newX = Mathf.Lerp(currentX, targetXPosition, switchSpeed * Time.deltaTime);
            movement.x = newX - currentX;

            // Gravity & jumping
            if (controller.isGrounded)
            {
                if (isJumping)
                    isJumping = false;
                verticalVelocity = -1f;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            movement.y = verticalVelocity * Time.deltaTime;

            controller.Move(movement);

            // Body tilt animation
            UpdateBodyTilt();
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

            // Jump
            if (jumpAction.WasPressedThisFrame() && controller.isGrounded)
                Jump();

            // Touch swipe (using Touchscreen from Input System)
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                // Store start position handled via pointer
            }
        }

        private void Jump()
        {
            verticalVelocity = jumpForce;
            isJumping = true;
            speedDebt += jumpSpeedPenalty;
        }

        private void UpdateBodyTilt()
        {
            if (bodyTransform == null) return;

            // Tilt backward slightly while rising, forward while falling
            float targetTilt = 0f;
            if (isJumping || !controller.isGrounded)
            {
                targetTilt = verticalVelocity > 0 ? -maxBodyTiltAngle : maxBodyTiltAngle * 0.5f;
            }

            currentBodyTilt = Mathf.Lerp(currentBodyTilt, targetTilt, bodyTiltSpeed * Time.deltaTime);
            bodyTransform.localRotation = Quaternion.Euler(currentBodyTilt, 0f, 0f);
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
