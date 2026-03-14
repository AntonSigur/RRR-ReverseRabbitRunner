using UnityEngine;

namespace ReverseRabbitRunner.PowerUps
{
    /// <summary>
    /// Birth-Carrot: Spawns baby rabbits in ALL 5 lanes that auto-collect carrots.
    /// Babies die to obstacles or the farmer. No duration — they persist until killed.
    /// </summary>
    public class BirthCarrot : PowerUpBase
    {
        [Header("Birth Carrot")]
        [SerializeField] private float laneWidth = 3f;
        [SerializeField] private int laneCount = 5;

        protected override void Activate(Player.RabbitController rabbit)
        {
            isActive = true;

            var farmer = Object.FindFirstObjectByType<Enemies.FarmerController>();
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");

            // Spawn a baby in every lane
            for (int lane = 0; lane < laneCount; lane++)
            {
                // Stagger them slightly in Z so they don't all spawn on top of each other
                float zOffset = 1.5f + lane * 0.4f;

                BabyRabbit.CreateBabyRabbit(
                    lane, laneWidth, laneCount,
                    rabbit, farmer, zOffset, urpLit);
            }

            Debug.Log($"[BirthCarrot] Spawned {laneCount} baby rabbits!");

            // Destroy the power-up object after activation
            Destroy(gameObject);
        }

        protected override void Deactivate(Player.RabbitController rabbit)
        {
            isActive = false;
        }
    }
}
