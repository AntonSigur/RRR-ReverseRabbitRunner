using System.Collections.Generic;
using UnityEngine;

namespace ReverseRabbitRunner.Player
{
    /// <summary>
    /// Sits on a wider trigger collider parented to the rabbit. Tracks every
    /// "Obstacle" that enters this zone; when an obstacle leaves the zone
    /// without having caused a stumble (and is still alive — i.e. wasn't
    /// destroyed by a successful jump), it counts as a near-miss.
    ///
    /// Fires <see cref="OnNearMiss"/> with the obstacle that was dodged.
    /// Listeners (CameraFollow shake, ScoreManager bonus, HUD label) wire up
    /// in RabbitController.
    /// </summary>
    public class NearMissDetector : MonoBehaviour
    {
        public RabbitController rabbit;
        /// <summary>Stumble within this many seconds of obstacle exit invalidates the near-miss.</summary>
        public float stumbleGraceSeconds = 0.4f;
        public System.Action<GameObject> OnNearMiss;

        private readonly Dictionary<int, float> entered = new();

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Obstacle")) return;
            entered[other.GetInstanceID()] = Time.time;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Obstacle")) return;
            int id = other.GetInstanceID();
            if (!entered.Remove(id)) return;

            if (rabbit == null || !rabbit.IsAlive) return;
            // If we just stumbled, this obstacle WAS the hit — not a near-miss
            if (Time.time - rabbit.LastStumbleTime < stumbleGraceSeconds) return;

            OnNearMiss?.Invoke(other.gameObject);
        }
    }
}
