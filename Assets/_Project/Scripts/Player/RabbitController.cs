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

        [Header("Stumble Settings")]
        [SerializeField] private float stumbleSpeedPenaltySmall = 2.0f;
        [SerializeField] private float stumbleSpeedPenaltyTall = 4.0f;
        [SerializeField] private float stumbleRecoveryTime = 1.5f;
        [SerializeField] private float stumbleDangerWindow = 7.0f;

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
        private bool isStumbling;
        private float stumbleTimer;
        private float lastStumbleTime = -100f;
        private float stumbleShakeTimer;

        // Flight state
        private bool isFlying;
        private float? flightTargetY;

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
        public bool IsStumbling => isStumbling;
        public bool IsFlying => isFlying;
        public bool InDangerWindow => (Time.time - lastStumbleTime) < stumbleDangerWindow;
        public float LastStumbleTime => lastStumbleTime;

        public event System.Action OnHitObstacle;
        public event System.Action<float> OnStumble;
        public event System.Action<GameObject> OnCollectCarrot;
        /// <summary>Fires when the rabbit dodges or jump-clears an obstacle without stumbling.</summary>
        public event System.Action<GameObject> OnNearMiss;

        /// <summary>
        /// Single-source-of-truth pickup hook. Used by both direct trigger collection
        /// and the Magnet-Carrot auto-collect so events / score / combo all flow
        /// through the same code path.
        /// </summary>
        public void NotifyCarrotCollected(GameObject carrot)
        {
            OnCollectCarrot?.Invoke(carrot);
            Core.ScoreManager.Instance?.AddScore(1);
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            currentLane = laneCount / 2;
            baseSpeed = forwardSpeed;
            UpdateTargetPosition();

            // Find "Body" child for tilt animation
            var body = transform.Find("Body");
            if (body != null) bodyTransform = body;

            BuildNearMissZone();
            BuildInputActions();
            ApplyEasterEggVisuals();
        }

        // Materials we created at runtime — destroyed in OnDestroy to avoid leaks.
        private System.Collections.Generic.List<Material> easterEggMaterials;

        /// <summary>
        /// Tints the rabbit body / head / paws with a gold sheen when the
        /// <see cref="Core.EasterEggs.GoldenRabbitUnlocked"/> cheat is active.
        ///
        /// Walks every Renderer under the Body container and finds the shared
        /// <c>Rabbit_Mat</c> material (assigned in <see cref="Editor.SceneSetup"/>).
        /// We swap that single material for a tinted instance so the head, body
        /// and limbs all change colour together; eyes / nose / pupils keep their
        /// original materials. The tint is multiplicative so URP lighting still
        /// reads correctly.
        /// </summary>
        private void ApplyEasterEggVisuals()
        {
            if (!Core.EasterEggs.GoldenRabbitUnlocked) return;
            if (bodyTransform == null) return;

            var renderers = bodyTransform.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers == null || renderers.Length == 0) return;

            easterEggMaterials = new System.Collections.Generic.List<Material>();
            Material tintedShared = null;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;

                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    if (!m.name.StartsWith("Rabbit_Mat")) continue;

                    if (tintedShared == null)
                    {
                        tintedShared = new Material(m) { name = "Rabbit_Mat_Golden" };
                        if (tintedShared.HasProperty("_BaseColor"))
                            tintedShared.SetColor("_BaseColor", Core.EasterEggs.GoldenRabbitTint);
                        else
                            tintedShared.color = Core.EasterEggs.GoldenRabbitTint;
                        easterEggMaterials.Add(tintedShared);
                    }
                    mats[i] = tintedShared;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        private void BuildNearMissZone()
        {
            // Wider trigger zone surrounding the rabbit — detects obstacles that
            // pass close by without actually colliding. A NearMissDetector child
            // forwards "obstacle exited zone alive" events as OnNearMiss.
            var zone = new GameObject("NearMissZone");
            zone.transform.SetParent(transform, false);
            zone.transform.localPosition = Vector3.zero;
            zone.layer = gameObject.layer;

            var box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            // Wider than rabbit's collider but narrower than two lanes — so an obstacle
            // in an *adjacent* lane only registers a near-miss if the player swerved
            // close to the boundary, not if they were comfortably one full lane over.
            box.size = new Vector3(4.4f, 2.6f, 1.8f);
            box.center = new Vector3(0f, 0.6f, 0f);

            // A trigger needs a Rigidbody on one of the participants to fire OnTrigger
            // events with CharacterController-only objects. Mark it kinematic so it
            // doesn't influence physics.
            var rb = zone.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var detector = zone.AddComponent<NearMissDetector>();
            detector.rabbit = this;
            detector.OnNearMiss += go => OnNearMiss?.Invoke(go);
        }

        private void BuildInputActions()
        {
            // Lane switching input — built once at Awake, enabled/disabled
            // by OnEnable/OnDisable to avoid per-cycle allocation churn.
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

            jumpAction = new InputAction("Jump", InputActionType.Button);
            jumpAction.AddBinding("<Keyboard>/space");
            jumpAction.AddBinding("<Keyboard>/w");
            jumpAction.AddBinding("<Keyboard>/upArrow");
            jumpAction.AddBinding("<Gamepad>/buttonSouth");
        }

        private void OnEnable()
        {
            moveAction?.Enable();
            jumpAction?.Enable();
            SubscribeTouchInput();
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            jumpAction?.Disable();
            UnsubscribeTouchInput();
        }

        private bool touchInputBound;

        private void SubscribeTouchInput()
        {
            var im = Core.InputManager.Instance;
            if (im == null || touchInputBound) return;
            im.OnSwipeLeft  += HandleSwipeLeft;
            im.OnSwipeRight += HandleSwipeRight;
            im.OnSwipeUp    += HandleTouchJump;
            im.OnTap        += HandleTouchJump;
            touchInputBound = true;
        }

        private void UnsubscribeTouchInput()
        {
            var im = Core.InputManager.Instance;
            if (im == null || !touchInputBound) return;
            im.OnSwipeLeft  -= HandleSwipeLeft;
            im.OnSwipeRight -= HandleSwipeRight;
            im.OnSwipeUp    -= HandleTouchJump;
            im.OnTap        -= HandleTouchJump;
            touchInputBound = false;
        }

        private void HandleSwipeLeft()
        {
            if (!isAlive) return;
            MoveLeft();
        }

        private void HandleSwipeRight()
        {
            if (!isAlive) return;
            MoveRight();
        }

        private void HandleTouchJump()
        {
            if (!isAlive) return;
            if (controller != null && controller.isGrounded && !isFlying)
                Jump();
        }

        private void OnDestroy()
        {
            moveAction?.Dispose();
            jumpAction?.Dispose();

            if (easterEggMaterials != null)
            {
                for (int i = 0; i < easterEggMaterials.Count; i++)
                    if (easterEggMaterials[i] != null) Destroy(easterEggMaterials[i]);
                easterEggMaterials = null;
            }
        }

        private void Update()
        {
            // Lazy subscribe — InputManager auto-spawns via RuntimeInitializeOnLoad,
            // which may race with this controller's OnEnable on first scene load.
            if (!touchInputBound) SubscribeTouchInput();

            if (!isAlive) return;

            // DEBUG: Shift+1 = instant death (farmer catches up and kills)
            #if UNITY_EDITOR
            if ((Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed && Keyboard.current.digit1Key.wasPressedThisFrame)
                || (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.Alpha1)))
            {
                Debug.Log("[DEBUG] Shift+1: Triggering instant death sequence");
                // Move farmer right next to rabbit
                var farmerObj = GameObject.FindGameObjectWithTag("Farmer");
                if (farmerObj != null)
                    farmerObj.transform.position = transform.position + Vector3.forward * 1.5f;
                Die();
                return;
            }
            #endif

            // Gravity — disabled during flight
            if (isFlying)
            {
                verticalVelocity = 0;
            }
            else if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f; // Small downward force to stay grounded
                if (isJumping) isJumping = false;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            HandleInput();

            // Speed escalation: if a DifficultyManager is present it drives the target
            // (tier-based step-up curve). Otherwise fall back to the legacy linear drift
            // so the rabbit still works in test scenes without one.
            var diff = Core.DifficultyManager.Instance;
            if (diff != null)
            {
                float target = Mathf.Min(diff.SpeedTarget, maxSpeed);
                baseSpeed = Mathf.MoveTowards(baseSpeed, target, diff.SpeedRampPerSecond * Time.deltaTime);
            }
            else
            {
                baseSpeed = Mathf.Min(baseSpeed + speedIncreaseRate * Time.deltaTime, maxSpeed);
            }

            // Recover from speed debt (jump or stumble)
            if (speedDebt > 0f)
            {
                float recoveryRate = isStumbling
                    ? stumbleSpeedPenaltyTall / stumbleRecoveryTime
                    : jumpSpeedPenalty / jumpRecoveryTime;
                speedDebt = Mathf.Max(speedDebt - recoveryRate * Time.deltaTime, 0f);
            }

            // Stumble recovery timer
            if (isStumbling)
            {
                stumbleTimer -= Time.deltaTime;
                if (stumbleTimer <= 0f)
                    isStumbling = false;
            }

            forwardSpeed = Core.CheatConsole.SpeedOverride ?? Mathf.Max(baseSpeed - speedDebt, 2f);

            Vector3 movement = Vector3.zero;

            // Forward movement (rabbit runs backwards, so negative Z)
            movement.z = -forwardSpeed * Time.deltaTime;

            // Smooth lateral movement — full speed during flight, slower when airborne
            float switchSpeed = laneSwitchSpeed;
            if (!controller.isGrounded && !isFlying)
                switchSpeed *= airLaneSwitchMultiplier;

            float currentX = transform.position.x;
            float newX = Mathf.Lerp(currentX, targetXPosition, switchSpeed * Time.deltaTime);
            movement.x = newX - currentX;

            // Vertical movement: flight target or gravity
            if (isFlying && flightTargetY.HasValue)
            {
                float yDiff = flightTargetY.Value - transform.position.y;
                movement.y = yDiff * 8f * Time.deltaTime;
            }
            else
            {
                movement.y = verticalVelocity * Time.deltaTime;
            }

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

            if (jumpPressed && controller.isGrounded && !isFlying)
                Jump();
        }

        private void Jump()
        {
            verticalVelocity = jumpForce;
            isJumping = true;
            speedDebt += jumpSpeedPenalty;
            Core.AudioManager.Instance?.PlayJump();
            Debug.Log($"[Jump] force={jumpForce} speedDebt={speedDebt:F1}");
        }

        private void UpdateBodyTilt()
        {
            if (bodyTransform == null) return;
            if (isFlying) return; // FlightController manages rotation during flight

            float targetTilt = 0f;
            if (!controller.isGrounded)
            {
                targetTilt = verticalVelocity > 0 ? -maxBodyTiltAngle : maxBodyTiltAngle * 0.5f;
            }

            currentBodyTilt = Mathf.Lerp(currentBodyTilt, targetTilt, bodyTiltSpeed * Time.deltaTime);

            // Stumble shake wobble
            float shakeZ = 0f;
            if (stumbleShakeTimer > 0f)
            {
                stumbleShakeTimer -= Time.deltaTime;
                float intensity = Mathf.Clamp01(stumbleShakeTimer / stumbleRecoveryTime);
                shakeZ = Mathf.Sin(Time.time * 30f) * 8f * intensity;
            }

            Vector3 euler = bodyTransform.localEulerAngles;
            bodyTransform.localEulerAngles = new Vector3(currentBodyTilt, euler.y, shakeZ);
        }

        public void MoveLeft()
        {
            if (currentLane > 0)
            {
                currentLane--;
                UpdateTargetPosition();
                Core.AudioManager.Instance?.PlayLaneSwitch();
            }
        }

        public void MoveRight()
        {
            if (currentLane < laneCount - 1)
            {
                currentLane++;
                UpdateTargetPosition();
                Core.AudioManager.Instance?.PlayLaneSwitch();
            }
        }

        public void Die()
        {
            if (Core.CheatConsole.GodMode) return;
            if (!isAlive) return;
            isAlive = false;

            // Death sequence controller handles the cinematic + GameOver call.
            // If no death sequence is active (e.g. stumble-death), start one.
            var deathSeq = Object.FindAnyObjectByType<Core.DeathSequence>();
            if (deathSeq != null && !deathSeq.IsPlaying)
            {
                var farmer = GameObject.FindGameObjectWithTag("Farmer");
                if (farmer != null)
                {
                    deathSeq.Play(farmer.transform, transform);
                    return;
                }
            }
            else if (deathSeq != null && deathSeq.IsPlaying)
            {
                // Sequence already running (farmer-initiated)
                return;
            }

            // Fallback if no death sequence found
            Core.GameManager.Instance?.GameOver();
        }

        public void HitObstacle()
        {
            OnHitObstacle?.Invoke();
        }

        // === Flight Mode ===

        public void SetFlying(bool flying)
        {
            isFlying = flying;
            if (flying)
            {
                verticalVelocity = 0;
                isJumping = false;
            }
        }

        public void SetFlightTarget(float? targetY)
        {
            flightTargetY = targetY;
        }

        private void Stumble(float penalty)
        {
            if (Core.CheatConsole.GodMode) return;

            // Two stumbles within danger window = death
            if ((Time.time - lastStumbleTime) < stumbleDangerWindow)
            {
                Die();
                return;
            }

            lastStumbleTime = Time.time;
            speedDebt += penalty;
            isStumbling = true;
            stumbleTimer = stumbleRecoveryTime;
            stumbleShakeTimer = stumbleRecoveryTime;

            OnStumble?.Invoke(penalty);
            OnHitObstacle?.Invoke();
            // Stumble breaks the carrot streak
            Core.ScoreManager.Instance?.BreakCombo();
        }

        private void UpdateTargetPosition()
        {
            int centerLane = laneCount / 2;
            targetXPosition = (currentLane - centerLane) * laneWidth;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isAlive) return;

            if (other.CompareTag("Carrot"))
            {
                NotifyCarrotCollected(other.gameObject);
                Destroy(other.gameObject);
            }
            else if (other.CompareTag("Obstacle"))
            {
                if (isFlying) return; // Invulnerable during flight
                float obstacleHeight = other.bounds.size.y;
                bool isSmall = obstacleHeight < 1.0f;

                // Height-based jump clearance: if rabbit's feet are above the obstacle, clear it
                if (isJumping && !controller.isGrounded)
                {
                    float rabbitFeetY = transform.position.y;
                    float obstacleTopY = other.bounds.max.y;

                    if (rabbitFeetY >= obstacleTopY - 0.2f)
                    {
                        OnNearMiss?.Invoke(other.gameObject);
                        if (isSmall) Destroy(other.gameObject);
                        return;
                    }
                }

                // Read obstacle bounds BEFORE disabling the collider
                Bounds obstacleBounds = other.bounds;
                other.enabled = false;

                // Push rabbit in front of the obstacle (rabbit runs in -Z, so push to +Z edge)
                float obstacleEdgeZ = obstacleBounds.max.z + controller.radius + 0.15f;
                Vector3 pos = transform.position;
                if (pos.z < obstacleEdgeZ)
                {
                    pos.z = obstacleEdgeZ;
                    transform.position = pos;
                }

                float penalty = isSmall ? stumbleSpeedPenaltySmall : stumbleSpeedPenaltyTall;
                Stumble(penalty);
            }
            else if (other.CompareTag("PowerUp"))
            {
                var powerUp = other.GetComponent<PowerUps.PowerUpBase>();
                if (powerUp != null)
                    powerUp.Collect(this);
            }
        }
    }
}
