using UnityEngine;

namespace ReverseRabbitRunner.Enemies
{
    /// <summary>
    /// Angry farmer who chases the rabbit with obstacle avoidance AI.
    /// Dodges obstacles by switching lanes (imperfect), stumbles on hits,
    /// fades out when far behind, reappears after delay.
    /// Flight-aware: runs freely while rabbit is flying.
    /// </summary>
    public class FarmerController : MonoBehaviour
    {
        [Header("Chase Settings")]
        [SerializeField] private float baseDistance = 5f;
        [SerializeField] private float catchDistance = 1f;
        [SerializeField] private float farmerRecoveryTime = 10f;

        [Header("Rabbit Stumble Penalty")]
        [SerializeField] private float smallObstaclePenaltyDistance = 2f;
        [SerializeField] private float tallObstaclePenaltyDistance = 4f;

        [Header("Obstacle Avoidance AI")]
        [SerializeField] private float lookAheadDistance = 5f;
        [SerializeField] private float scanInterval = 0.5f;
        [SerializeField] private float dodgeSuccessRate = 0.40f;
        [SerializeField] private float laneWidth = 3f;
        [SerializeField] private int laneCount = 5;

        [Header("Farmer Stumble")]
        [SerializeField] private float farmerStumbleDuration = 2.5f;
        [SerializeField] private float farmerStumblePenalty = 10f;
        [SerializeField] private float fadeDistance = 25f;
        [SerializeField] private float maxDistance = 35f;
        [SerializeField] private float reappearDelay = 8f;

        [Header("Death Sequence")]
        [SerializeField] private float catchPauseDuration = 1.0f;

        [Header("Lateral Movement")]
        [SerializeField] private float lateralFollowSpeed = 3f;
        [SerializeField] private float swayAmount = 0.3f;
        [SerializeField] private float swaySpeed = 2f;

        [Header("References")]
        [SerializeField] private Transform playerTransform;

        // Chase state
        private float currentDistance;
        private float currentX;
        private float swayOffset;
        private bool isCatching;
        private float catchTimer;
        private Player.RabbitController rabbitController;
        private Core.DeathSequence deathSequence;
        private Rigidbody rb;

        // Obstacle avoidance state
        private int targetLane;
        private float nextScanTime;

        // Farmer stumble state
        private bool isFarmerStumbling;
        private float farmerStumbleTimer;
        private float tooFarTimer;
        private bool isVisible = true;
        private bool isReappearing;
        private int stumbleCount;

        // === Public State (for debug overlay & tests) ===
        public float CurrentDistance => currentDistance;
        public float BaseDistance => baseDistance;
        public float FadeDistance => fadeDistance;
        public float MaxDistance => maxDistance;
        public float NormalizedThreat => 1f - Mathf.Clamp01(currentDistance / baseDistance);
        public bool IsStumbling => isFarmerStumbling;
        public bool IsVisible => isVisible;
        public bool IsCatching => isCatching;
        public bool IsReappearing => isReappearing;
        public int TargetLane => targetLane;
        public float StumbleTimer => farmerStumbleTimer;
        public float TooFarTimer => tooFarTimer;
        public int StumbleCount => stumbleCount;

        public event System.Action OnCaughtRabbit;

        /// <summary>Force stumble from external code (tests, console).</summary>
        public void ForceStumble() => FarmerStumble();

        /// <summary>Force distance (tests).</summary>
        public void ForceDistance(float dist) => currentDistance = dist;

        private void Start()
        {
            currentDistance = baseDistance;
            targetLane = laneCount / 2;

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
                    rabbitController.OnStumble += OnRabbitStumble;
            }

            // Kinematic rigidbody for trigger detection
            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Trigger collider for obstacle contact
            var col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.8f, 2f, 0.8f);
            col.center = new Vector3(0, 1f, 0);

            // Remove visual-only colliders from children
            foreach (var childCol in GetComponentsInChildren<Collider>())
                if (childCol.gameObject != gameObject) Destroy(childCol);
        }

        private void Update()
        {
            if (playerTransform == null) return;
            if (Core.GameManager.Instance?.CurrentState != Core.GameManager.GameState.Playing) return;

            if (isCatching)
            {
                HandleCatchSequence();
                return;
            }

            bool rabbitFlying = rabbitController?.IsFlying == true;

            // Obstacle scanning — skip during flight (farmer runs freely)
            if (Time.time >= nextScanTime && !rabbitFlying)
            {
                ScanAndDodge();
                nextScanTime = Time.time + scanInterval;
            }

            // Recover from farmer stumble
            if (isFarmerStumbling)
            {
                farmerStumbleTimer -= Time.deltaTime;
                if (farmerStumbleTimer <= 0f)
                    isFarmerStumbling = false;
            }

            // Z distance: recover toward baseDistance
            float recoveryRate = (baseDistance - catchDistance) / Mathf.Max(farmerRecoveryTime, 0.1f);
            if (isFarmerStumbling)
                recoveryRate *= 0.2f;
            else if (rabbitFlying)
                recoveryRate *= 2f; // Farmer runs freely during flight — faster recovery
            currentDistance = Mathf.MoveTowards(currentDistance, baseDistance, recoveryRate * Time.deltaTime);

            // Lateral movement
            float laneX = (targetLane - laneCount / 2) * laneWidth;
            float rabbitX = playerTransform.position.x;
            float blendTarget = isFarmerStumbling
                ? currentX
                : Mathf.Lerp(laneX, rabbitX, 0.6f);
            currentX = Mathf.Lerp(currentX, blendTarget, lateralFollowSpeed * Time.deltaTime);

            // Sway
            swayOffset = isFarmerStumbling
                ? Mathf.Sin(Time.time * swaySpeed * 4f) * swayAmount * 3f
                : Mathf.Sin(Time.time * swaySpeed) * swayAmount;

            // Position farmer
            Vector3 farmerPos = new Vector3(
                currentX + swayOffset, 0f,
                playerTransform.position.z + currentDistance);
            transform.position = farmerPos;

            // Obstacle proximity check — skip during flight
            if (!isFarmerStumbling && !isCatching && !rabbitFlying)
            {
                Vector3 boxCenter = farmerPos + Vector3.up;
                Vector3 halfExtents = new Vector3(0.5f, 1f, 0.75f);
                var hits = Physics.OverlapBox(boxCenter, halfExtents, Quaternion.identity,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
                foreach (var hit in hits)
                {
                    if (hit.CompareTag("Obstacle"))
                    {
                        FarmerStumble();
                        break;
                    }
                }
            }

            // Face rabbit (looks up when rabbit is flying)
            Vector3 lookDir = playerTransform.position - farmerPos;
            if (lookDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(lookDir);

            // Visibility
            bool shouldBeVisible = currentDistance < fadeDistance;
            if (shouldBeVisible != isVisible)
            {
                isVisible = shouldBeVisible;
                foreach (var r in GetComponentsInChildren<Renderer>())
                    r.enabled = isVisible;
            }

            // Reappear logic: once past maxDistance, timer starts and won't reset
            if (currentDistance > maxDistance && !isReappearing)
            {
                isReappearing = true;
                tooFarTimer = 0f;
            }

            if (isReappearing)
            {
                if (currentDistance < fadeDistance)
                {
                    // Natural recovery brought farmer back — cancel reappear
                    isReappearing = false;
                    tooFarTimer = 0f;
                }
                else
                {
                    tooFarTimer += Time.deltaTime;
                    if (tooFarTimer >= reappearDelay)
                    {
                        currentDistance = baseDistance;
                        tooFarTimer = 0f;
                        isReappearing = false;
                        isFarmerStumbling = false;
                        targetLane = rabbitController?.CurrentLane ?? laneCount / 2;

                        if (!isVisible)
                        {
                            isVisible = true;
                            foreach (var r in GetComponentsInChildren<Renderer>())
                                r.enabled = true;
                        }
                        Debug.Log("[Farmer] Reappeared!");
                    }
                }
            }

            // Catch check — not during stumble, flight, or god mode
            if (currentDistance <= catchDistance && !isFarmerStumbling
                && !Core.CheatConsole.GodMode && !rabbitFlying)
                StartCatchSequence();
        }

        // === Obstacle Avoidance AI ===

        private void ScanAndDodge()
        {
            if (isFarmerStumbling) return;

            float myX = (targetLane - laneCount / 2) * laneWidth;
            Vector3 scanCenter = new Vector3(
                myX, 1f, transform.position.z - lookAheadDistance * 0.5f);
            Vector3 halfExtents = new Vector3(laneWidth * 0.4f, 1.5f, lookAheadDistance * 0.5f);

            bool obstacleAhead = false;
            var cols = Physics.OverlapBox(scanCenter, halfExtents, Quaternion.identity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            foreach (var col in cols)
            {
                if (col.CompareTag("Obstacle")) { obstacleAhead = true; break; }
            }
            if (!obstacleAhead) return;

            if (Random.value > dodgeSuccessRate) return;

            int rabbitLane = rabbitController?.CurrentLane ?? laneCount / 2;
            int left = targetLane > 0 ? targetLane - 1 : -1;
            int right = targetLane < laneCount - 1 ? targetLane + 1 : -1;

            int first = -1, second = -1;
            if (left >= 0 && right >= 0)
            {
                if (Mathf.Abs(left - rabbitLane) <= Mathf.Abs(right - rabbitLane))
                    { first = left; second = right; }
                else
                    { first = right; second = left; }
            }
            else if (left >= 0) first = left;
            else if (right >= 0) first = right;

            if (first >= 0 && !IsLaneBlocked(first))
                targetLane = first;
            else if (second >= 0 && !IsLaneBlocked(second))
                targetLane = second;
        }

        private bool IsLaneBlocked(int lane)
        {
            float laneX = (lane - laneCount / 2) * laneWidth;
            Vector3 center = new Vector3(
                laneX, 1f, transform.position.z - lookAheadDistance * 0.5f);
            Vector3 halfExtents = new Vector3(laneWidth * 0.4f, 1.5f, lookAheadDistance * 0.5f);

            var cols = Physics.OverlapBox(center, halfExtents, Quaternion.identity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            foreach (var col in cols)
            {
                if (col.CompareTag("Obstacle")) return true;
            }
            return false;
        }

        // === Collision ===

        private void OnTriggerEnter(Collider other)
        {
            if (isCatching || isFarmerStumbling) return;
            if (rabbitController?.IsFlying == true) return;

            if (other.CompareTag("Obstacle"))
                FarmerStumble();
        }

        private void FarmerStumble()
        {
            isFarmerStumbling = true;
            farmerStumbleTimer = farmerStumbleDuration;
            currentDistance += farmerStumblePenalty;
            stumbleCount++;
            Debug.Log($"[Farmer] Stumbled #{stumbleCount}! Distance: {currentDistance:F1}");
        }

        // === Rabbit Stumble Response ===

        public void OnRabbitStumble(float severity)
        {
            if (isFarmerStumbling) return;
            float penalty = severity > 3f ? tallObstaclePenaltyDistance : smallObstaclePenaltyDistance;
            currentDistance = Mathf.Max(catchDistance, currentDistance - penalty);
        }

        // === Catch Sequence ===

        private void HandleCatchSequence()
        {
            if (deathSequence != null && deathSequence.IsPlaying) return;

            catchTimer -= Time.deltaTime;
            Vector3 catchPos = new Vector3(
                playerTransform.position.x, 0f,
                playerTransform.position.z + catchDistance);
            transform.position = catchPos;

            Vector3 catchLook = playerTransform.position - transform.position;
            catchLook.y = 0;
            if (catchLook.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(catchLook);

            if (catchTimer <= 0f)
            {
                OnCaughtRabbit?.Invoke();
                deathSequence = FindAnyObjectByType<Core.DeathSequence>();
                if (deathSequence == null)
                {
                    var go = new GameObject("[DeathSequence]");
                    deathSequence = go.AddComponent<Core.DeathSequence>();
                }
                deathSequence.Play(transform, rabbitController.transform);
            }
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
                rabbitController.OnStumble -= OnRabbitStumble;
        }
    }
}
