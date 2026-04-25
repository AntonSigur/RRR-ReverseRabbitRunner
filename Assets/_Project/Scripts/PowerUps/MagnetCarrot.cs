using UnityEngine;

namespace ReverseRabbitRunner.PowerUps
{
    /// <summary>
    /// Magnet-Carrot: For a few seconds, normal carrots within a radius around the
    /// rabbit accelerate toward it and auto-collect on close approach. Boosts
    /// combo-streak feasibility — chained collects are easy, x5 multiplier is reachable.
    /// </summary>
    public class MagnetCarrot : PowerUpBase
    {
        [Header("Magnet Settings")]
        [SerializeField] private float magnetDuration = 6f;
        [SerializeField] private float magnetRadius = 12f;

        protected override void Activate(Player.RabbitController rabbit)
        {
            isActive = true;

            var existing = rabbit.GetComponent<MagnetEffect>();
            if (existing != null)
            {
                // Stack/refresh: extend duration instead of adding a second component
                existing.Refresh(magnetDuration, magnetRadius);
                Debug.Log("[MagnetCarrot] Refreshed existing magnet.");
            }
            else
            {
                var me = rabbit.gameObject.AddComponent<MagnetEffect>();
                me.Initialize(rabbit, magnetDuration, magnetRadius);
                Debug.Log("[MagnetCarrot] Activated! Carrots will home in.");
            }

            Destroy(gameObject);
        }

        protected override void Deactivate(Player.RabbitController rabbit)
        {
            isActive = false;
        }
    }
}
