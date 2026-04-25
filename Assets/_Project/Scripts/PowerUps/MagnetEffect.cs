using System.Collections.Generic;
using UnityEngine;

namespace ReverseRabbitRunner.PowerUps
{
    /// <summary>
    /// Runtime carrier for the Magnet-Carrot power-up. Lives on the rabbit while
    /// active, scans for nearby carrots each frame, accelerates them toward the
    /// rabbit and auto-collects on close approach (counts toward the combo streak
    /// just like a manual pickup).
    ///
    /// Self-destroys when the timer expires.
    /// </summary>
    public class MagnetEffect : MonoBehaviour
    {
        private Player.RabbitController rabbit;
        private float remaining;
        private float originalDuration;
        private float radius;
        private float pullSpeedMax = 28f;        // peak homing speed (units/sec)
        private float collectDistance = 1.4f;    // auto-pick threshold

        private static readonly Collider[] hits = new Collider[32];

        // Per-carrot momentum so the home-in feels physical, not jumpy
        private readonly Dictionary<int, float> carrotSpeeds = new();

        // Static so HUD / DebugOverlay can show remaining time
        public static MagnetEffect Active { get; private set; }
        public float Remaining => remaining;
        public float DurationFraction => originalDuration <= 0f ? 0f : Mathf.Clamp01(remaining / originalDuration);

        public void Initialize(Player.RabbitController r, float duration, float radiusUnits)
        {
            rabbit = r;
            remaining = duration;
            originalDuration = duration;
            radius = radiusUnits;
            Active = this;
        }

        public void Refresh(float duration, float radiusUnits)
        {
            // Extend rather than stack. Take the max so refresh never shortens the buff.
            remaining = Mathf.Max(remaining, duration);
            originalDuration = Mathf.Max(originalDuration, duration);
            radius = Mathf.Max(radius, radiusUnits);
        }

        private void Update()
        {
            if (rabbit == null || !rabbit.IsAlive)
            {
                EndEffect();
                return;
            }

            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                EndEffect();
                return;
            }

            PullCarrots();
        }

        private void PullCarrots()
        {
            Vector3 rabbitPos = rabbit.transform.position;

            int count = Physics.OverlapSphereNonAlloc(rabbitPos, radius, hits);
            for (int i = 0; i < count; i++)
            {
                var col = hits[i];
                if (col == null) continue;
                // Only pull plain carrots — power-up carrots (BirthCarrot/WingCarrot/MagnetCarrot)
                // shouldn't be auto-grabbed; the player should choose to pick them.
                if (!col.CompareTag("Carrot")) continue;

                int id = col.GetInstanceID();
                Vector3 toRabbit = rabbitPos - col.transform.position;
                float dist = toRabbit.magnitude;

                if (dist <= collectDistance)
                {
                    // Auto-collect: route through ScoreManager so it counts for the combo
                    rabbit.NotifyCarrotCollected(col.gameObject);
                    carrotSpeeds.Remove(id);
                    Destroy(col.gameObject);
                    continue;
                }

                // Ramp homing speed up as the carrot gets closer (feels juicy)
                float t = 1f - Mathf.Clamp01(dist / radius);
                float targetSpeed = Mathf.Lerp(6f, pullSpeedMax, t * t);

                if (!carrotSpeeds.TryGetValue(id, out float speed)) speed = 0f;
                speed = Mathf.MoveTowards(speed, targetSpeed, 30f * Time.deltaTime);
                carrotSpeeds[id] = speed;

                Vector3 dir = toRabbit / Mathf.Max(dist, 0.0001f);
                col.transform.position += dir * speed * Time.deltaTime;
            }

            // Periodic light cleanup of dead IDs
            if (carrotSpeeds.Count > 64)
                carrotSpeeds.Clear();
        }

        private void EndEffect()
        {
            if (Active == this) Active = null;
            Destroy(this);
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }
    }
}
