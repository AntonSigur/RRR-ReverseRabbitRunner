using UnityEngine;

namespace ReverseRabbitRunner.PowerUps
{
    /// <summary>
    /// Manages the Wing-Carrot flight sequence:
    /// TransitionUp (180° backflip + rise) → Flying (carrot stream) → TransitionDown (180° backflip + descend)
    /// </summary>
    public class FlightController : MonoBehaviour
    {
        [SerializeField] private float flightDuration = 8f;
        [SerializeField] private float flightHeight = 6f;
        [SerializeField] private float transitionDuration = 1.0f;
        [SerializeField] private float carrotSpacing = 2f;

        private Player.RabbitController rabbit;
        private Player.CameraFollow cameraFollow;
        private float phaseTimer;
        private float startY;
        private Quaternion preFlightRotation;
        private FlightPhase phase;
        private GameObject carrotStreamParent;

        private enum FlightPhase { TransitionUp, Flying, TransitionDown }

        public bool IsActive => phase != FlightPhase.TransitionDown || phaseTimer < transitionDuration;

        public void Initialize(Player.RabbitController rabbit, float height, float duration)
        {
            this.rabbit = rabbit;
            this.flightHeight = height;
            this.flightDuration = duration;

            cameraFollow = Object.FindAnyObjectByType<Player.CameraFollow>();

            startY = rabbit.transform.position.y;
            preFlightRotation = rabbit.transform.rotation;

            phase = FlightPhase.TransitionUp;
            phaseTimer = 0f;

            rabbit.SetFlying(true);
            rabbit.SetFlightTarget(startY);
            if (cameraFollow != null) cameraFollow.SetFlightMode(true);

            Debug.Log("[Flight] Transition UP — backflip!");
        }

        private void Update()
        {
            if (rabbit == null || !rabbit.IsAlive)
            {
                Cleanup();
                return;
            }

            if (Core.GameManager.Instance?.CurrentState != Core.GameManager.GameState.Playing)
                return;

            phaseTimer += Time.deltaTime;

            switch (phase)
            {
                case FlightPhase.TransitionUp:
                    UpdateTransitionUp();
                    break;
                case FlightPhase.Flying:
                    UpdateFlying();
                    break;
                case FlightPhase.TransitionDown:
                    UpdateTransitionDown();
                    break;
            }
        }

        private void UpdateTransitionUp()
        {
            float t = Mathf.Clamp01(phaseTimer / transitionDuration);
            float eased = Mathf.SmoothStep(0, 1, t);

            // Height: ground → flight height
            float targetY = Mathf.Lerp(startY, flightHeight, eased);
            rabbit.SetFlightTarget(targetY);

            // 360° backflip (pitch) + 180° turn (yaw) → ends facing forward, right-side up
            float pitch = eased * -360f;
            float yaw = eased * 180f;
            rabbit.transform.rotation = Quaternion.Euler(pitch, yaw, 0);

            if (t >= 1f)
            {
                phase = FlightPhase.Flying;
                phaseTimer = 0f;
                rabbit.transform.rotation = Quaternion.Euler(0, 180f, 0);
                rabbit.SetFlightTarget(flightHeight);
                SpawnCarrotStream();
                Debug.Log("[Flight] FLYING! Collecting sky carrots...");
            }
        }

        private void UpdateFlying()
        {
            rabbit.SetFlightTarget(flightHeight);
            rabbit.transform.rotation = Quaternion.Euler(0, 180f, 0);

            if (phaseTimer >= flightDuration)
            {
                phase = FlightPhase.TransitionDown;
                phaseTimer = 0f;
                Debug.Log("[Flight] Transition DOWN — backflip return!");
            }
        }

        private void UpdateTransitionDown()
        {
            float t = Mathf.Clamp01(phaseTimer / transitionDuration);
            float eased = Mathf.SmoothStep(0, 1, t);

            // Height: flight → ground
            float targetY = Mathf.Lerp(flightHeight, 1f, eased);
            rabbit.SetFlightTarget(targetY);

            // Another 360° backflip + 180° turn back to facing +Z (backward)
            float pitch = eased * -360f;
            float yaw = 180f + eased * 180f;
            rabbit.transform.rotation = Quaternion.Euler(pitch, yaw, 0);

            if (t >= 1f)
            {
                Cleanup();
            }
        }

        private void SpawnCarrotStream()
        {
            float speed = rabbit.CurrentSpeed;
            float totalDist = speed * flightDuration;
            int carrotCount = Mathf.CeilToInt(totalDist / carrotSpacing);

            carrotStreamParent = new GameObject("[FlightCarrots]");

            // Lane change points (2-3 changes)
            int numChanges = Random.Range(2, 4);
            float[] changePositions = new float[numChanges];
            for (int i = 0; i < numChanges; i++)
                changePositions[i] = Random.Range(0.15f, 0.85f);
            System.Array.Sort(changePositions);

            int currentLane = Random.Range(0, 5);
            int changeIdx = 0;
            float startZ = rabbit.transform.position.z;
            float laneWidth = 3f;
            int laneCount = 5;

            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Material carrotMat = MakeMat(new Color(1f, 0.5f, 0.05f), urpLit);
            Material leavesMat = MakeMat(new Color(0.1f, 0.6f, 0.1f), urpLit);

            for (int i = 0; i < carrotCount; i++)
            {
                float fraction = (float)i / carrotCount;

                if (changeIdx < numChanges && fraction >= changePositions[changeIdx])
                {
                    int newLane;
                    do { newLane = Random.Range(0, laneCount); } while (newLane == currentLane);
                    currentLane = newLane;
                    changeIdx++;
                }

                float x = (currentLane - laneCount / 2) * laneWidth;
                float z = startZ - (i * carrotSpacing);

                var carrot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                carrot.name = "FlightCarrot";
                carrot.tag = "Carrot";
                carrot.transform.parent = carrotStreamParent.transform;
                carrot.transform.position = new Vector3(x, flightHeight + 0.44f, z);
                carrot.transform.localScale = new Vector3(0.34f, 0.68f, 0.34f);
                carrot.transform.rotation = Quaternion.Euler(0, 0, 180f);
                carrot.GetComponent<Renderer>().material = carrotMat;
                carrot.GetComponent<Collider>().isTrigger = true;

                var leaves = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leaves.name = "Leaves";
                leaves.transform.parent = carrot.transform;
                leaves.transform.localPosition = new Vector3(0, -0.7f, 0);
                leaves.transform.localScale = new Vector3(2f, 0.3f, 2f);
                Object.DestroyImmediate(leaves.GetComponent<Collider>());
                leaves.GetComponent<Renderer>().material = leavesMat;
            }

            Debug.Log($"[Flight] Spawned {carrotCount} sky carrots across {numChanges + 1} lane segments");
        }

        private void Cleanup()
        {
            if (rabbit != null)
            {
                rabbit.SetFlying(false);
                rabbit.SetFlightTarget(null);
                rabbit.transform.rotation = preFlightRotation;
            }

            if (cameraFollow != null)
                cameraFollow.SetFlightMode(false);

            if (carrotStreamParent != null)
                Destroy(carrotStreamParent);

            Debug.Log("[Flight] Flight complete — back to backwards running!");
            Destroy(this);
        }

        private static Material MakeMat(Color color, Shader shader)
        {
            var mat = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
            mat.SetColor("_BaseColor", color);
            return mat;
        }
    }
}
