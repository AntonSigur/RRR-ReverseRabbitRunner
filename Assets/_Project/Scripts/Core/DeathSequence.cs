using UnityEngine;
using System.Collections;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Dramatic game-over death sequence: farmer catches and kills rabbit.
    /// Multi-stage cinematic: approach → raise fork → stab → rabbit falls → fade.
    /// Camera orbits 360° around the scene during the sequence.
    /// Particle effects: blood drops (default) or carrot pieces (family-friendly toggle).
    /// </summary>
    public class DeathSequence : MonoBehaviour
    {
        [Header("Timing (seconds)")]
        [SerializeField] private float approachDuration = 2.0f;
        [SerializeField] private float raiseForkDuration = 1.5f;
        [SerializeField] private float stabDuration = 0.8f;
        [SerializeField] private float rabbitFallDuration = 1.5f;
        [SerializeField] private float lingerDuration = 1.5f;
        [SerializeField] private float fadeOutDuration = 1.0f;

        [Header("Camera")]
        [SerializeField] private float orbitRadius = 4f;
        [SerializeField] private float orbitHeight = 2.5f;
        [SerializeField] private float orbitSpeed = 360f / 7.3f; // Full rotation over sequence

        [Header("Slow Motion")]
        [SerializeField] private float slowMotionScale = 0.3f;

        [Header("Particles")]
        [SerializeField] private int particleBurstCount = 40;
        [SerializeField] private float particleSpread = 3f;
        [SerializeField] private float particleLifetime = 2.5f;

        // Blood mode (true) vs Carrot mode (false)
        public static bool UseBloodParticles
        {
            get => PlayerPrefs.GetInt("DeathParticleMode", 1) == 1;
            set => PlayerPrefs.SetInt("DeathParticleMode", value ? 1 : 0);
        }

        private Transform farmerTransform;
        private Transform rabbitTransform;
        private Transform farmerForkPivot;
        private Camera mainCamera;
        private Transform originalCamParent;
        private float orbitAngle;
        private bool isPlaying;
        private float fadeAlpha;
        private GameObject[] particles;

        public bool IsPlaying => isPlaying;

        public float TotalDuration =>
            approachDuration + raiseForkDuration + stabDuration +
            rabbitFallDuration + lingerDuration + fadeOutDuration;

        /// <summary>
        /// Start the dramatic death sequence. Called by FarmerController when catching the rabbit.
        /// </summary>
        public void Play(Transform farmer, Transform rabbit)
        {
            if (isPlaying) return;

            farmerTransform = farmer;
            rabbitTransform = rabbit;
            mainCamera = Camera.main;

            // Find the fork pivot on the farmer for animation
            farmerForkPivot = FindDeep(farmer, "RightForkPivot") ?? FindDeep(farmer, "ForkPivot");

            // Disable fork wave animation so it doesn't fight the death sequence
            var forkWave = farmer.GetComponent<Enemies.FarmerForkWave>();
            if (forkWave != null) forkWave.enabled = false;

            // Disable mirror cameras (mirrors go dark immediately)
            DisableMirrorCameras();

            // Detach main camera from CameraFollow
            if (mainCamera != null)
            {
                var camFollow = mainCamera.GetComponent<Player.CameraFollow>();
                if (camFollow != null) camFollow.enabled = false;
                originalCamParent = mainCamera.transform.parent;
                mainCamera.transform.SetParent(null);
            }

            isPlaying = true;
            fadeAlpha = 0f;
            orbitAngle = 0f;

            StartCoroutine(RunSequence());
        }

        private IEnumerator RunSequence()
        {
            // Use unscaled time since we set timeScale
            Time.timeScale = slowMotionScale;

            Vector3 sceneCenter = rabbitTransform.position;
            float elapsed;

            // === STAGE 1: Menacing approach ===
            Vector3 farmerStart = farmerTransform.position;
            Vector3 farmerEnd = rabbitTransform.position + (farmerTransform.position - rabbitTransform.position).normalized * 1.2f;

            elapsed = 0f;
            while (elapsed < approachDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / approachDuration);

                // Farmer slowly approaches
                farmerTransform.position = Vector3.Lerp(farmerStart, farmerEnd, t);
                FaceFarmerToRabbit();

                // Orbit camera
                UpdateOrbitCamera(sceneCenter);

                yield return null;
            }

            // === STAGE 2: Raise fork ===
            elapsed = 0f;
            Quaternion forkStartRot = farmerForkPivot != null ? farmerForkPivot.localRotation : Quaternion.identity;
            Quaternion forkRaisedRot = forkStartRot * Quaternion.Euler(-90f, 0, 0); // Fork raised high

            while (elapsed < raiseForkDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / raiseForkDuration);

                if (farmerForkPivot != null)
                    farmerForkPivot.localRotation = Quaternion.Slerp(forkStartRot, forkRaisedRot, t);

                FaceFarmerToRabbit();
                UpdateOrbitCamera(sceneCenter);

                yield return null;
            }

            // === STAGE 3: STAB! ===
            elapsed = 0f;
            Quaternion forkStabRot = forkStartRot * Quaternion.Euler(30f, 0, 0); // Fork thrust forward/down
            Vector3 farmerLungeTarget = rabbitTransform.position + Vector3.up * 0.3f +
                (farmerTransform.position - rabbitTransform.position).normalized * 0.5f;

            while (elapsed < stabDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / stabDuration;

                // Quick stab motion
                float stabT = t < 0.4f ? Mathf.SmoothStep(0, 1, t / 0.4f) : 1f;
                if (farmerForkPivot != null)
                    farmerForkPivot.localRotation = Quaternion.Slerp(forkRaisedRot, forkStabRot, stabT);

                // Farmer lunges slightly
                farmerTransform.position = Vector3.Lerp(farmerEnd, farmerLungeTarget, stabT * 0.5f);
                FaceFarmerToRabbit();
                UpdateOrbitCamera(sceneCenter);

                yield return null;
            }

            // Spawn particles on impact!
            SpawnDeathParticles(rabbitTransform.position + Vector3.up * 0.5f);

            // === STAGE 4: Rabbit falls ===
            elapsed = 0f;
            Vector3 rabbitStart = rabbitTransform.position;
            Quaternion rabbitStartRot = rabbitTransform.rotation;
            // Find the Body child for the fall animation
            Transform bodyChild = rabbitTransform.Find("Body");

            while (elapsed < rabbitFallDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / rabbitFallDuration);

                // Rabbit tips over sideways
                if (bodyChild != null)
                {
                    bodyChild.localRotation = Quaternion.Euler(0, 0, t * 90f);
                    bodyChild.localPosition = new Vector3(0, -t * 0.3f, 0);
                }

                FaceFarmerToRabbit();
                UpdateOrbitCamera(sceneCenter);

                yield return null;
            }

            // === STAGE 5: Linger ===
            elapsed = 0f;
            while (elapsed < lingerDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                UpdateOrbitCamera(sceneCenter);
                yield return null;
            }

            // === STAGE 6: Fade out ===
            elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeAlpha = Mathf.Clamp01(elapsed / fadeOutDuration);
                UpdateOrbitCamera(sceneCenter);
                yield return null;
            }

            // Sequence complete — restore time and trigger game over UI
            Time.timeScale = 0f;
            isPlaying = false;

            GameManager.Instance?.GameOver();
        }

        private void FaceFarmerToRabbit()
        {
            if (farmerTransform == null || rabbitTransform == null) return;
            Vector3 dir = rabbitTransform.position - farmerTransform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
                farmerTransform.rotation = Quaternion.LookRotation(dir);
        }

        private void UpdateOrbitCamera(Vector3 center)
        {
            if (mainCamera == null) return;

            orbitAngle += orbitSpeed * Time.unscaledDeltaTime;

            float rad = orbitAngle * Mathf.Deg2Rad;
            Vector3 camPos = center + new Vector3(
                Mathf.Sin(rad) * orbitRadius,
                orbitHeight,
                Mathf.Cos(rad) * orbitRadius
            );

            mainCamera.transform.position = camPos;
            mainCamera.transform.LookAt(center + Vector3.up * 0.5f);
        }

        private void DisableMirrorCameras()
        {
            // Disable ALL mirror cameras (Camera components with "Mirror" in name)
            var allCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in allCameras)
            {
                if (cam.name.Contains("Mirror"))
                {
                    cam.enabled = false;
                    cam.gameObject.SetActive(false);
                }
            }

            // Disable the MirrorCamera controller script so it stops adjusting
            var mirrorController = rabbitTransform.GetComponent<Player.MirrorCamera>();
            if (mirrorController != null) mirrorController.enabled = false;

            // Hide mirror quads (the visible glass surfaces)
            HideChildRenderers(rabbitTransform, "Mirror");
        }

        private void HideChildRenderers(Transform root, string nameContains)
        {
            foreach (Transform child in root)
            {
                if (child.name.Contains(nameContains))
                {
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer != null) renderer.enabled = false;
                }
                HideChildRenderers(child, nameContains);
            }
        }

        private void SpawnDeathParticles(Vector3 origin)
        {
            bool useBlood = UseBloodParticles;
            particles = new GameObject[particleBurstCount];

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Universal Render Pipeline/Unlit");

            for (int i = 0; i < particleBurstCount; i++)
            {
                GameObject p;
                if (useBlood)
                {
                    // Blood drop: visible red elongated sphere
                    p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    p.name = "BloodDrop";
                    float scale = Random.Range(0.06f, 0.2f);
                    p.transform.localScale = new Vector3(scale, scale * 2f, scale);
                    var mat = new Material(urpLit);
                    mat.color = new Color(
                        Random.Range(0.7f, 1.0f),
                        Random.Range(0.0f, 0.08f),
                        Random.Range(0.0f, 0.05f)
                    );
                    mat.SetFloat("_Smoothness", 0.9f);
                    p.GetComponent<Renderer>().material = mat;
                }
                else
                {
                    // Carrot piece: orange/green cylinders
                    p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    p.name = "CarrotPiece";
                    float scale = Random.Range(0.06f, 0.18f);
                    p.transform.localScale = new Vector3(scale, scale * 2.5f, scale);
                    var mat = new Material(urpLit);
                    bool isGreen = Random.value > 0.7f; // 30% green tops
                    mat.color = isGreen
                        ? new Color(0.2f, Random.Range(0.5f, 0.8f), 0.1f)
                        : new Color(Random.Range(0.85f, 1f), Random.Range(0.3f, 0.55f), Random.Range(0.0f, 0.1f));
                    p.GetComponent<Renderer>().material = mat;
                }

                Object.DestroyImmediate(p.GetComponent<Collider>());
                p.transform.position = origin + Random.insideUnitSphere * 0.15f;

                // Physics: compensate for slow-motion by using stronger forces
                var rb = p.AddComponent<Rigidbody>();
                rb.mass = 0.01f;
                rb.useGravity = true;
                float forceMultiplier = 1f / Mathf.Max(slowMotionScale, 0.1f);
                Vector3 force = new Vector3(
                    Random.Range(-particleSpread, particleSpread),
                    Random.Range(3f, particleSpread * 2f),
                    Random.Range(-particleSpread, particleSpread)
                ) * forceMultiplier;
                rb.AddForce(force, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 15f * forceMultiplier, ForceMode.Impulse);

                particles[i] = p;
                Destroy(p, particleLifetime / Mathf.Max(slowMotionScale, 0.1f));
            }
        }

        private void OnGUI()
        {
            if (!isPlaying && fadeAlpha <= 0f) return;

            // Fade-to-black overlay
            if (fadeAlpha > 0f)
            {
                GUI.color = new Color(0, 0, 0, fadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
