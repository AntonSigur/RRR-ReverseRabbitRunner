using UnityEngine;
using System.Collections.Generic;

namespace ReverseRabbitRunner.PowerUps
{
    /// <summary>
    /// Chaotic baby rabbit that runs alongside the main rabbit.
    /// Wanders randomly across lanes, jumps, collects carrots on contact.
    /// Spawned by the Birth-Carrot power-up. Dies on obstacle hit or farmer catch.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BabyRabbit : MonoBehaviour
    {
        [SerializeField] private float deathPopForce = 5f;

        private Transform rabbitTransform;
        private Player.RabbitController rabbitController;
        private Enemies.FarmerController farmerController;
        private bool isAlive = true;

        // Chaotic movement
        private float speedMultiplier;
        private float initialZOffset;
        private float wanderTargetX;
        private float nextWanderTime;
        private float nextJumpTime;
        private float verticalVelocity;
        private float hopPhaseOffset;
        private float currentY;

        private const float Gravity = -15f;
        private const float JumpForce = 4f;
        private const float GroundY = 0.7f;
        private const float MaxWanderX = 7f;
        private const float WanderSpeed = 3.5f;

        public static readonly List<BabyRabbit> ActiveBabies = new();

        public void Initialize(Player.RabbitController rabbit, Enemies.FarmerController farmer,
            float zOffset, float xStart, float speedMult)
        {
            rabbitTransform = rabbit.transform;
            rabbitController = rabbit;
            farmerController = farmer;
            speedMultiplier = speedMult;
            initialZOffset = zOffset;
            hopPhaseOffset = Random.Range(0f, Mathf.PI * 2f);
            currentY = GroundY;

            transform.position = new Vector3(xStart, GroundY, rabbitTransform.position.z + zOffset);

            wanderTargetX = Random.Range(-MaxWanderX, MaxWanderX);
            nextWanderTime = Time.time + Random.Range(0.3f, 1.5f);
            nextJumpTime = Time.time + Random.Range(1f, 6f);

            ActiveBabies.Add(this);
        }

        private void Update()
        {
            if (!isAlive || rabbitTransform == null) return;

            float dt = Time.deltaTime;
            Vector3 pos = transform.position;

            // Z: follow rabbit with rubber-banding, speed variance creates natural drift
            float targetZ = rabbitTransform.position.z + initialZOffset;
            pos.z = Mathf.Lerp(pos.z, targetZ, (1.5f + speedMultiplier) * dt);

            // X: chaotic wandering across all lanes
            if (Time.time >= nextWanderTime)
            {
                wanderTargetX = Random.Range(-MaxWanderX, MaxWanderX);
                nextWanderTime = Time.time + Random.Range(0.3f, 1.5f);
            }
            pos.x = Mathf.MoveTowards(pos.x, wanderTargetX, WanderSpeed * speedMultiplier * dt);

            // Y: gravity + random jumps like real babies
            if (Time.time >= nextJumpTime && currentY <= GroundY + 0.05f)
            {
                verticalVelocity = JumpForce * Random.Range(0.6f, 1.3f);
                nextJumpTime = Time.time + Random.Range(1.5f, 7f);
            }
            verticalVelocity += Gravity * dt;
            currentY += verticalVelocity * dt;
            if (currentY <= GroundY)
            {
                currentY = GroundY;
                verticalVelocity = 0f;
            }

            // Hop animation when grounded
            float hop = currentY <= GroundY + 0.05f
                ? Mathf.Abs(Mathf.Sin(Time.time * 8f + hopPhaseOffset)) * 0.12f
                : 0f;

            pos.y = currentY + hop;
            transform.position = pos;

            // Farmer proximity check
            if (farmerController != null)
            {
                Vector3 fp = farmerController.transform.position;
                if (Mathf.Abs(fp.z - pos.z) < 1.5f && Mathf.Abs(fp.x - pos.x) < 2f)
                    Die("farmer");
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
        /// Creates a chaotic baby rabbit (70% scale, random rainbow color).
        /// </summary>
        public static GameObject CreateBabyRabbit(int index,
            Player.RabbitController rabbit, Enemies.FarmerController farmer,
            float zOffset, float xStart, float speedMult, Shader urpLit)
        {
            var root = new GameObject($"BabyRabbit_{index}");
            root.layer = 0;

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Bigger collider for the 2x larger baby
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(0.8f, 1.0f, 0.8f);
            col.center = new Vector3(0, 0.1f, 0);
            col.isTrigger = true;

            float s = 0.7f; // 2x the old 0.35 scale

            // Random rainbow color per baby
            Color bodyColor = Color.HSVToRGB(
                Random.value, Random.Range(0.5f, 0.85f), Random.Range(0.8f, 1f));

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

            // Eyes
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

            var baby = root.AddComponent<BabyRabbit>();
            baby.Initialize(rabbit, farmer, zOffset, xStart, speedMult);

            return root;
        }
    }
}
