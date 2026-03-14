using UnityEngine;
using System.Collections.Generic;

namespace ReverseRabbitRunner.PowerUps
{
    /// <summary>
    /// Baby rabbit that runs alongside the main rabbit, auto-collecting carrots in its lane.
    /// Spawned by the Birth-Carrot power-up. Dies on obstacle hit or farmer catch.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BabyRabbit : MonoBehaviour
    {
        [SerializeField] private float deathPopForce = 5f;

        private int assignedLane;
        private float laneWidth;
        private int laneCount;
        private Transform rabbitTransform;
        private Player.RabbitController rabbitController;
        private Enemies.FarmerController farmerController;
        private float targetX;
        private bool isAlive = true;
        private float zOffset; // stays slightly behind main rabbit

        // Track active babies for the power-up to manage
        public static readonly List<BabyRabbit> ActiveBabies = new();

        public void Initialize(int lane, float laneW, int lanes,
            Player.RabbitController rabbit, Enemies.FarmerController farmer, float behindOffset)
        {
            assignedLane = lane;
            laneWidth = laneW;
            laneCount = lanes;
            rabbitTransform = rabbit.transform;
            rabbitController = rabbit;
            farmerController = farmer;
            zOffset = behindOffset;

            targetX = (lane - laneCount / 2) * laneWidth;
            transform.position = new Vector3(targetX, 0.4f, rabbitTransform.position.z + zOffset);

            ActiveBabies.Add(this);
        }

        private void Update()
        {
            if (!isAlive || rabbitTransform == null) return;

            float speed = rabbitController.CurrentSpeed;

            // Follow rabbit in Z (stay at fixed offset behind)
            Vector3 pos = transform.position;
            float targetZ = rabbitTransform.position.z + zOffset;
            pos.z = Mathf.Lerp(pos.z, targetZ, 8f * Time.deltaTime);

            // Stay in lane
            pos.x = Mathf.Lerp(pos.x, targetX, 10f * Time.deltaTime);
            pos.y = 0.4f;

            transform.position = pos;

            // Hop animation
            float hop = Mathf.Abs(Mathf.Sin(Time.time * 8f + assignedLane)) * 0.15f;
            transform.localPosition = new Vector3(pos.x, 0.4f + hop, pos.z);

            // Check farmer proximity
            if (farmerController != null)
            {
                float farmerZ = farmerController.transform.position.z;
                float farmerX = farmerController.transform.position.x;
                float distZ = Mathf.Abs(farmerZ - pos.z);
                float distX = Mathf.Abs(farmerX - pos.x);
                if (distZ < 1.5f && distX < 2f)
                {
                    Die("farmer");
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isAlive) return;

            if (other.CompareTag("Carrot"))
            {
                Core.ScoreManager.Instance?.AddScore(1);
                Core.AudioManager.Instance?.PlayCollectCarrot();
                Destroy(other.gameObject);
            }
            else if (other.CompareTag("Obstacle"))
            {
                Die("obstacle");
            }
        }

        private void Die(string cause)
        {
            if (!isAlive) return;
            isAlive = false;
            ActiveBabies.Remove(this);

            Debug.Log($"[BabyRabbit] Lane {assignedLane} died: {cause}");

            // Pop effect: scatter body parts
            foreach (Transform child in transform)
            {
                var rb = child.gameObject.AddComponent<Rigidbody>();
                rb.mass = 0.1f;
                rb.AddForce(
                    Random.insideUnitSphere * deathPopForce + Vector3.up * deathPopForce,
                    ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 10f);
                child.parent = null;
                Destroy(child.gameObject, 1.5f);
            }

            Destroy(gameObject, 0.1f);
        }

        private void OnDestroy()
        {
            ActiveBabies.Remove(this);
        }

        /// <summary>
        /// Creates a cute mini rabbit visual (35% scale of the adult).
        /// Returns the root GameObject with BabyRabbit component attached.
        /// </summary>
        public static GameObject CreateBabyRabbit(int lane, float laneW, int lanes,
            Player.RabbitController rabbit, Enemies.FarmerController farmer,
            float behindOffset, Shader urpLit)
        {
            var root = new GameObject($"BabyRabbit_Lane{lane}");
            root.layer = 0;

            // Rigidbody for trigger detection
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Trigger collider
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(0.4f, 0.6f, 0.4f);
            col.center = new Vector3(0, 0.1f, 0);
            col.isTrigger = true;

            float s = 0.35f; // scale factor

            // Pastel colors per lane
            Color[] babyColors = {
                new(1f, 0.85f, 0.85f),   // pink
                new(0.85f, 0.92f, 1f),   // blue
                new(1f, 1f, 0.85f),       // yellow
                new(0.85f, 1f, 0.88f),   // mint
                new(0.95f, 0.85f, 1f)    // lavender
            };
            Color bodyColor = babyColors[lane % babyColors.Length];

            System.Func<Color, Material> mat = (c) =>
            {
                var m = new Material(urpLit);
                m.SetColor("_BaseColor", c);
                return m;
            };
            Material bodyMat = mat(bodyColor);
            Material earMat = mat(new Color(1f, 0.75f, 0.8f));
            Material noseMat = mat(new Color(1f, 0.4f, 0.5f));
            Material eyeMat = mat(new Color(0.1f, 0.05f, 0f));

            // Body
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.parent = root.transform;
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(0.6f * s, 0.5f * s, 0.55f * s);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().material = bodyMat;

            // Head
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.parent = root.transform;
            head.transform.localPosition = new Vector3(0, 0.55f * s, 0);
            head.transform.localScale = new Vector3(0.5f * s, 0.45f * s, 0.45f * s);
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().material = bodyMat;

            // Ears
            for (int side = -1; side <= 1; side += 2)
            {
                var ear = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ear.name = side < 0 ? "EarL" : "EarR";
                ear.transform.parent = root.transform;
                ear.transform.localPosition = new Vector3(side * 0.06f, 0.95f * s + 0.1f, 0);
                ear.transform.localScale = new Vector3(0.12f * s, 0.45f * s, 0.08f * s);
                ear.transform.localRotation = Quaternion.Euler(0, 0, side * -15f);
                Object.DestroyImmediate(ear.GetComponent<Collider>());
                ear.GetComponent<Renderer>().material = earMat;
            }

            // Eyes (simple dots)
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = side < 0 ? "EyeL" : "EyeR";
                eye.transform.parent = root.transform;
                eye.transform.localPosition = new Vector3(side * 0.06f, 0.55f * s + 0.02f, -0.07f);
                eye.transform.localScale = Vector3.one * 0.04f;
                Object.DestroyImmediate(eye.GetComponent<Collider>());
                eye.GetComponent<Renderer>().material = eyeMat;
            }

            // Nose
            var nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nose.name = "Nose";
            nose.transform.parent = root.transform;
            nose.transform.localPosition = new Vector3(0, 0.55f * s - 0.01f, -0.08f);
            nose.transform.localScale = Vector3.one * 0.03f;
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.GetComponent<Renderer>().material = noseMat;

            // Tail
            var tail = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tail.name = "Tail";
            tail.transform.parent = root.transform;
            tail.transform.localPosition = new Vector3(0, 0, 0.12f);
            tail.transform.localScale = Vector3.one * 0.08f;
            Object.DestroyImmediate(tail.GetComponent<Collider>());
            tail.GetComponent<Renderer>().material = bodyMat;

            // Initialize the component
            var baby = root.AddComponent<BabyRabbit>();
            baby.Initialize(lane, laneW, lanes, rabbit, farmer, behindOffset);

            return root;
        }
    }
}
