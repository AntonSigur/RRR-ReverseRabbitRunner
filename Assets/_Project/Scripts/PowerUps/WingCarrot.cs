using UnityEngine;

namespace ReverseRabbitRunner.PowerUps
{
    /// <summary>
    /// Wing-Carrot: Rabbit turns forward, sprouts wings, and flies/glides
    /// to collect airborne carrots.
    /// </summary>
    public class WingCarrot : PowerUpBase
    {
        [Header("Wing Carrot Settings")]
        [SerializeField] private float flyHeight = 5f;
        [SerializeField] private float glideSpeed = 15f;

        protected override void Activate(Player.RabbitController rabbit)
        {
            isActive = true;
            // TODO: Flip rabbit to face forward, elevate, enable flight controls
            Debug.Log("Wing-Carrot activated! Rabbit takes flight!");
        }

        protected override void Deactivate(Player.RabbitController rabbit)
        {
            isActive = false;
            // TODO: Return rabbit to ground, flip back to backwards
            Debug.Log("Wing-Carrot effect ended. Back to running backwards.");
        }
    }
}
