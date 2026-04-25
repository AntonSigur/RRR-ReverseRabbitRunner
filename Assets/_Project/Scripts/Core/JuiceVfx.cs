using UnityEngine;
using ReverseRabbitRunner.Player;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Lightweight VFX juice singleton. Auto-spawns. Owns three procedural
    /// ParticleSystems:
    ///   - Speed lines (intensity scales with CurrentSpeed).
    ///   - Carrot-collect sparkle burst (fired from OnCollectCarrot).
    ///   - Landing dust puff (fired the frame the rabbit goes airborne→grounded).
    /// All resources are created in code so the system needs no prefabs.
    /// </summary>
    public class JuiceVfx : MonoBehaviour
    {
        public static JuiceVfx Instance { get; private set; }

        private RabbitController rabbit;
        private bool subscribed;
        private bool wasGrounded = true;

        private ParticleSystem speedLines;
        private ParticleSystem sparkle;
        private ParticleSystem dust;
        private Camera cachedCam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (Instance != null) return;
            var go = new GameObject("[JuiceVfx]");
            DontDestroyOnLoad(go);
            go.AddComponent<JuiceVfx>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildSpeedLines();
            BuildSparkle();
            BuildDust();
        }

        private void Update()
        {
            if (!subscribed || rabbit == null)
            {
                if (rabbit == null) subscribed = false;
                TrySubscribe();
            }

            UpdateSpeedLines();
            UpdateLandingDust();
        }

        private void TrySubscribe()
        {
            if (subscribed) return;
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            rabbit = p.GetComponent<RabbitController>();
            if (rabbit == null) return;
            rabbit.OnCollectCarrot += OnCollectCarrot;
            subscribed = true;
        }

        private void OnDestroy()
        {
            if (rabbit != null) rabbit.OnCollectCarrot -= OnCollectCarrot;
        }

        // --- Speed Lines ---

        private void BuildSpeedLines()
        {
            var go = new GameObject("SpeedLines");
            go.transform.SetParent(transform, false);
            speedLines = go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 6f;
            r.velocityScale = 0f;
            r.material = new Material(Shader.Find("Sprites/Default"));

            var main = speedLines.main;
            main.startLifetime = 0.25f;
            main.startSpeed = 0f;
            main.startSize = 0.06f;
            main.startColor = new Color(1f, 1f, 1f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            var emission = speedLines.emission;
            emission.rateOverTime = 0f;

            var shape = speedLines.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(8f, 5f, 0.1f);

            var velocity = speedLines.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.z = 28f; // streaks fly toward player (positive Z = into camera)

            var color = speedLines.colorOverLifetime;
            color.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.3f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(grad);
        }

        private void UpdateSpeedLines()
        {
            if (rabbit == null || !rabbit.IsAlive) { SetSpeedLineRate(0f); return; }
            float s = rabbit.CurrentSpeed;
            // Visible above ~10 u/s, ramps up to a cap of ~22 u/s
            float intensity = Mathf.Clamp01((s - 10f) / 12f);
            // Anchor in front of camera (player runs backwards toward +Z)
            if (cachedCam == null) cachedCam = Camera.main;
            var cam = cachedCam;
            if (cam != null)
            {
                var t = speedLines.transform;
                t.position = cam.transform.position + cam.transform.forward * 6f;
                t.rotation = cam.transform.rotation;
            }
            SetSpeedLineRate(intensity * 80f);
        }

        private void SetSpeedLineRate(float rate)
        {
            var em = speedLines.emission;
            em.rateOverTime = rate;
        }

        // --- Sparkle ---

        private void BuildSparkle()
        {
            var go = new GameObject("CollectSparkle");
            go.transform.SetParent(transform, false);
            sparkle = go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.material = new Material(Shader.Find("Sprites/Default"));

            var main = sparkle.main;
            main.startLifetime = 0.45f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.7f, 0.25f), new Color(1f, 0.95f, 0.6f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            main.gravityModifier = 0.6f;

            var emission = sparkle.emission;
            emission.rateOverTime = 0f;

            var shape = sparkle.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var color = sparkle.colorOverLifetime;
            color.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f),
                        new GradientColorKey(new Color(1f, 0.55f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(grad);
        }

        private void OnCollectCarrot(GameObject carrot)
        {
            if (sparkle == null) return;
            var pos = carrot != null ? carrot.transform.position
                                     : (rabbit != null ? rabbit.transform.position : transform.position);
            sparkle.transform.position = pos + Vector3.up * 0.5f;
            sparkle.Emit(18);
        }

        // --- Landing Dust ---

        private void BuildDust()
        {
            var go = new GameObject("LandingDust");
            go.transform.SetParent(transform, false);
            dust = go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.material = new Material(Shader.Find("Sprites/Default"));

            var main = dust.main;
            main.startLifetime = 0.55f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.45f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.78f, 0.7f, 0.55f, 0.65f), new Color(0.88f, 0.82f, 0.7f, 0.65f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            main.gravityModifier = -0.05f;

            var emission = dust.emission;
            emission.rateOverTime = 0f;

            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.25f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var color = dust.colorOverLifetime;
            color.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.95f, 0.9f, 0.78f), 0f),
                        new GradientColorKey(new Color(0.7f, 0.65f, 0.5f), 1f) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(grad);
        }

        private void UpdateLandingDust()
        {
            if (rabbit == null || !rabbit.IsAlive) { wasGrounded = true; return; }
            bool grounded = rabbit.IsGrounded;
            if (grounded && !wasGrounded)
            {
                dust.transform.position = rabbit.transform.position + Vector3.down * 0.05f;
                dust.Emit(14);
            }
            wasGrounded = grounded;
        }
    }
}
