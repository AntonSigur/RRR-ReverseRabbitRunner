using UnityEngine;

namespace ReverseRabbitRunner.PowerUps
{
    /// <summary>
    /// Birth-Carrot: Spawns baby rabbits in ALL 5 lanes that auto-collect carrots.
    /// Babies die to obstacles or the farmer. No duration — they persist until killed.
    /// </summary>
    public class BirthCarrot : PowerUpBase
    {

        protected override void Activate(Player.RabbitController rabbit)
        {
            isActive = true;

            var farmer = Object.FindFirstObjectByType<Enemies.FarmerController>();
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");

            // Spawn a chaotic swarm of 125 rainbow babies
            int count = 125;
            for (int i = 0; i < count; i++)
            {
                float zOffset = Random.Range(-5f, 10f);
                float xStart = Random.Range(-7f, 7f);
                float speedMult = Random.Range(0.8f, 1.2f);

                BabyRabbit.CreateBabyRabbit(i, rabbit, farmer,
                    zOffset, xStart, speedMult, urpLit);
            }

            Debug.Log($"[BirthCarrot] Spawned {count} chaotic baby rabbits!");

            Destroy(gameObject);
        }

        protected override void Deactivate(Player.RabbitController rabbit)
        {
            isActive = false;
        }
    }
}
