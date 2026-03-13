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

            Debug.Log($"[DeathSequence] Play() started. Farmer={farmer?.name}, Rabbit={rabbit?.name}, MainCam={mainCamera?.name}");

            // Find the fork pivot on the farmer for animation
            farmerForkPivot = FindDeep(farmer, "RightForkPivot") ?? FindDeep(farmer, "ForkPivot");
            Debug.Log($"[DeathSequence] ForkPivot found: {(farmerForkPivot != null ? farmerForkPivot.name : "NULL")}");

            // Disable farmer's normal chase behavior so it doesn't fight the sequence
            var farmerCtrl = farmer.GetComponent<Enemies.FarmerController>();
            if (farmerCtrl != null) farmerCtrl.enabled = false;

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
            Debug.Log("[DeathSequence] RunSequence coroutine started");
            // Use unscaled time since we set timeScale
            Time.timeScale = slowMotionScale;

            Vector3 sceneCenter = rabbitTransform.position;
            float elapsed;

            // === STAGE 1: Menacing approach ===
            // Position farmer at a fixed starting distance, approaching the rabbit
            Vector3 approachDir = (farmerTransform.position - rabbitTransform.position);
            approachDir.y = 0;
            if (approachDir.sqrMagnitude < 0.1f) approachDir = Vector3.forward;
            approachDir = approachDir.normalized;

            Vector3 farmerStart = rabbitTransform.position + approachDir * 5f;
            Vector3 farmerEnd = rabbitTransform.position + approachDir * 1.5f;
            farmerTransform.position = farmerStart;

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
            Quaternion forkRaisedRot = forkStartRot * Quaternion.Euler(-270f, 0, 0); // Fork raised way overhead

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
            Quaternion forkStabRot = forkStartRot * Quaternion.Euler(110f, 30f, 23f);
            Vector3 farmerLungeTarget = rabbitTransform.position + approachDir * 0.5f;

            while (elapsed < stabDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / stabDuration;

                // Quick stab motion
                float stabT = t < 0.4f ? Mathf.SmoothStep(0, 1, t / 0.4f) : 1f;
                if (farmerForkPivot != null)
                    farmerForkPivot.localRotation = Quaternion.Slerp(forkRaisedRot, forkStabRot, stabT);

                // Farmer lunges into the rabbit
                farmerTransform.position = Vector3.Lerp(farmerEnd, farmerLungeTarget, stabT);
                FaceFarmerToRabbit();
                UpdateOrbitCamera(sceneCenter);

                yield return null;
            }

            // Spawn particles on impact!
            Debug.Log($"[DeathSequence] STAB! Spawning {particleBurstCount} particles at {rabbitTransform.position + Vector3.up * 0.5f} (blood={UseBloodParticles})");
            SpawnDeathParticles(rabbitTransform.position + Vector3.up * 0.5f);

            // === STAGE 4: Rabbit falls, farmer steps back ===
            elapsed = 0f;
            Vector3 rabbitStart = rabbitTransform.position;
            Quaternion rabbitStartRot = rabbitTransform.rotation;
            Transform bodyChild = rabbitTransform.Find("Body");
            Vector3 farmerPullBack = rabbitTransform.position + approachDir * 2.5f;

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

                // Farmer steps back after the stab
                farmerTransform.position = Vector3.Lerp(farmerLungeTarget, farmerPullBack, t);
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

            // Sequence complete — clear fade overlay and let GameHUD show game over
            Time.timeScale = 0f;
            isPlaying = false;
            fadeAlpha = 0f; // Clear black overlay so GameHUD game over screen is visible

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
            int camDisabled = 0;
            // Disable ALL mirror cameras
            var allCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in allCameras)
            {
                if (cam == mainCamera) continue;
                if (cam.name.Contains("Mirror"))
                {
                    if (cam.targetTexture != null)
                    {
                        RenderTexture prev = RenderTexture.active;
                        RenderTexture.active = cam.targetTexture;
                        GL.Clear(true, true, Color.black);
                        RenderTexture.active = prev;
                    }
                    cam.enabled = false;
                    camDisabled++;
                }
            }
            Debug.Log($"[DeathSequence] Disabled {camDisabled} mirror cameras");

            // Disable the MirrorCamera controller script
            if (rabbitTransform != null)
            {
                var mirrorController = rabbitTransform.GetComponent<Player.MirrorCamera>();
                if (mirrorController != null)
                {
                    mirrorController.enabled = false;
                    Debug.Log("[DeathSequence] MirrorCamera controller disabled");
                }
            }

            // Disable all Glass quad renderers and mirror assembly objects
            if (rabbitTransform != null)
                DisableMirrorVisuals(rabbitTransform);
        }

        private int mirrorVisualsDisabled;
        private void DisableMirrorVisuals(Transform root)
        {
            foreach (Transform child in root)
            {
                if (child.name == "Glass" || child.name.Contains("MirrorAssembly") || child.name.Contains("MirrorCamera"))
                {
                    child.gameObject.SetActive(false);
                    mirrorVisualsDisabled++;
                    Debug.Log($"[DeathSequence] Disabled mirror visual: {child.name}");
                }
                DisableMirrorVisuals(child);
            }
        }

        private void SpawnDeathParticles(Vector3 origin)
        {
            bool useBlood = UseBloodParticles;
            particles = new GameObject[particleBurstCount];

            // Get a working material by cloning from an existing scene renderer
            // Shader.Find() often fails at runtime in URP due to shader stripping
            Material baseMaterial = GetSceneBaseMaterial();

            for (int i = 0; i < particleBurstCount; i++)
            {
                GameObject p;
                if (useBlood)
                {
                    p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    p.name = "BloodDrop";
                    float scale = Random.Range(0.1f, 0.3f);
                    p.transform.localScale = new Vector3(scale, scale * 2f, scale);
                    var mat = new Material(baseMaterial);
                    mat.color = new Color(
                        Random.Range(0.7f, 1.0f),
                        Random.Range(0.0f, 0.08f),
                        Random.Range(0.0f, 0.05f)
                    );
                    if (mat.HasProperty("_Smoothness"))
                        mat.SetFloat("_Smoothness", 0.9f);
                    p.GetComponent<Renderer>().material = mat;
                }
                else
                {
                    p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    p.name = "CarrotPiece";
                    float scale = Random.Range(0.06f, 0.18f);
                    p.transform.localScale = new Vector3(scale, scale * 2.5f, scale);
                    var mat = new Material(baseMaterial);
                    bool isGreen = Random.value > 0.7f;
                    mat.color = isGreen
                        ? new Color(0.2f, Random.Range(0.5f, 0.8f), 0.1f)
                        : new Color(Random.Range(0.85f, 1f), Random.Range(0.3f, 0.55f), Random.Range(0.0f, 0.1f));
                    p.GetComponent<Renderer>().material = mat;
                }

                Object.DestroyImmediate(p.GetComponent<Collider>());
                p.transform.position = origin + Random.insideUnitSphere * 0.15f;

                var rb = p.AddComponent<Rigidbody>();
                rb.mass = 0.1f;
                rb.useGravity = true;
                rb.linearDamping = 0.5f;
                // VelocityChange ignores mass — values are direct m/s
                Vector3 velocity = new Vector3(
                    Random.Range(-particleSpread, particleSpread),
                    Random.Range(2f, particleSpread * 1.5f),
                    Random.Range(-particleSpread, particleSpread)
                );
                rb.AddForce(velocity, ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.VelocityChange);

                particles[i] = p;
                Destroy(p, particleLifetime);
            }
            Debug.Log($"[DeathSequence] Spawned {particleBurstCount} particles. First at {particles[0]?.transform.position}");
        }

        /// <summary>
        /// Get a working base material by cloning from an existing scene renderer.
        /// This avoids Shader.Find() failures due to URP shader stripping at runtime.
        /// </summary>
        private Material GetSceneBaseMaterial()
        {
            var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var r in renderers)
            {
                if (r.material != null && r.material.shader != null
                    && r.material.shader.name.Contains("Universal Render Pipeline"))
                {
                    Debug.Log($"[DeathSequence] Using shader from '{r.name}': {r.material.shader.name}");
                    return new Material(r.material.shader);
                }
            }

            Debug.LogWarning("[DeathSequence] No URP shader found from scene renderers, trying Shader.Find...");
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null) return new Material(urpLit);

            Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (urpUnlit != null) return new Material(urpUnlit);

            Debug.LogWarning("[DeathSequence] Falling back to Standard shader");
            return new Material(Shader.Find("Standard"));
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
