using UnityEngine;

namespace ReverseRabbitRunner.PowerUps
{
    /// <summary>
    /// Wing-Carrot: Rabbit does a 180° backflip, flies forward at 6x height
    /// collecting a sky carrot stream, then backflips back to backwards running.
    /// </summary>
    public class WingCarrot : PowerUpBase
    {
        [Header("Wing Carrot Settings")]
        [SerializeField] private float flyHeight = 6f;
        [SerializeField] private float flyDuration = 8f;

        protected override void Activate(Player.RabbitController rabbit)
        {
            isActive = true;

            // Don't activate if already flying
            if (rabbit.IsFlying)
            {
                Debug.Log("[WingCarrot] Already flying — ignored.");
                Destroy(gameObject);
                return;
            }

            var fc = rabbit.gameObject.AddComponent<FlightController>();
            fc.Initialize(rabbit, flyHeight, flyDuration);

            Debug.Log("[WingCarrot] Activated! Rabbit takes flight!");
            Destroy(gameObject);
        }

        protected override void Deactivate(Player.RabbitController rabbit)
        {
            isActive = false;
        }
    }
}
