using UnityEngine;

namespace ReverseRabbitRunner.Player
{
    /// <summary>
    /// Controls the rabbit's lane-switching movement.
    /// The rabbit runs backwards automatically; player controls lateral movement.
    /// Supports keyboard (A/D, Left/Right arrows) and touch swipe input.
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
        [SerializeField] private float speedIncreaseRate = 0.1f;
        [SerializeField] private float maxSpeed = 30f;

        [Header("Physics")]
        [SerializeField] private float gravity = -30f;

        [Header("Touch Input")]
        [SerializeField] private float swipeThreshold = 50f;

        private CharacterController controller;
        private int currentLane;
        private float targetXPosition;
        private float verticalVelocity;
        private bool isAlive = true;

        // Input state
        private bool leftPressed;
        private bool rightPressed;
        private Vector2 touchStartPos;
        private bool isSwiping;

        public float CurrentSpeed => forwardSpeed;
        public bool IsAlive => isAlive;
        public int CurrentLane => currentLane;

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
            // Keyboard: A/D or Left/Right arrows
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                MoveLeft();
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                MoveRight();

            // Touch swipe
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        touchStartPos = touch.position;
                        isSwiping = true;
                        break;
                    case TouchPhase.Ended:
                        if (isSwiping)
                        {
                            float swipeX = touch.position.x - touchStartPos.x;
                            if (Mathf.Abs(swipeX) > swipeThreshold)
                            {
                                if (swipeX < 0) MoveLeft();
                                else MoveRight();
                            }
                            isSwiping = false;
                        }
                        break;
                    case TouchPhase.Canceled:
                        isSwiping = false;
                        break;
                }
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
