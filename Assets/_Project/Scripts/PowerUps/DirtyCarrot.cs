using UnityEngine;

namespace ReverseRabbitRunner.PowerUps
{
    /// <summary>
    /// Dirty-Carrot: Obscures the rabbit's mirror vision, making it
    /// harder to see obstacles approaching from behind.
    /// </summary>
    public class DirtyCarrot : PowerUpBase
    {
        [Header("Dirty Carrot Settings")]
        [SerializeField] private float obscureAmount = 0.7f;

        private Player.MirrorCamera mirrorCamera;

        protected override void Activate(Player.RabbitController rabbit)
        {
            isActive = true;
            mirrorCamera = rabbit.GetComponentInChildren<Player.MirrorCamera>();
            mirrorCamera?.ApplyDirtyEffect(duration, obscureAmount);
            Debug.Log("Dirty-Carrot! Mirror vision obscured!");
        }

        protected override void Deactivate(Player.RabbitController rabbit)
        {
            isActive = false;
            mirrorCamera?.ClearDirtyEffect();
            Debug.Log("Dirty-Carrot effect cleared.");
        }
    }
}
