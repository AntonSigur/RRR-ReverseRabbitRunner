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
        [SerializeField] private float jumpForce = 14f;
        [SerializeField] private float jumpSpeedPenalty = 1.2f;
        [SerializeField] private float jumpRecoveryTime = 0.7f;
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

            // Find "Body" child for tilt animation
            var body = transform.Find("Body");
            if (body != null) bodyTransform = body;
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

            // Gravity first — so grounded state is correct for input
            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f; // Small downward force to stay grounded
                if (isJumping) isJumping = false;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            HandleInput();

            // Gradually increase base speed
            baseSpeed = Mathf.Min(baseSpeed + speedIncreaseRate * Time.deltaTime, maxSpeed);

            // Recover from jump speed debt over jumpRecoveryTime
            if (speedDebt > 0f)
            {
                float recoveryRate = jumpSpeedPenalty / jumpRecoveryTime;
                speedDebt = Mathf.Max(speedDebt - recoveryRate * Time.deltaTime, 0f);
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

            // Jump — check with both new and old Input System for reliability
            bool jumpPressed = jumpAction.WasPressedThisFrame();
            if (!jumpPressed)
            {
                jumpPressed = Input.GetKeyDown(KeyCode.Space)
                           || Input.GetKeyDown(KeyCode.W)
                           || Input.GetKeyDown(KeyCode.UpArrow);
            }

            if (jumpPressed && controller.isGrounded)
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
            Debug.Log($"[Jump] force={jumpForce} speedDebt={speedDebt:F1}");
        }

        private void UpdateBodyTilt()
        {
            if (bodyTransform == null) return;

            float targetTilt = 0f;
            if (!controller.isGrounded)
            {
                // Tilt backward while rising, slightly forward while falling
                targetTilt = verticalVelocity > 0 ? -maxBodyTiltAngle : maxBodyTiltAngle * 0.5f;
            }

            currentBodyTilt = Mathf.Lerp(currentBodyTilt, targetTilt, bodyTiltSpeed * Time.deltaTime);

            // Preserve existing Y/Z rotation, only modify X tilt
            Vector3 euler = bodyTransform.localEulerAngles;
            bodyTransform.localEulerAngles = new Vector3(currentBodyTilt, euler.y, euler.z);
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
