using UnityEngine;

namespace ReverseRabbitRunner.PowerUps
{
    /// <summary>
    /// Birth-Carrot: Spawns baby rabbits that run alongside in all lanes,
    /// collecting carrots automatically. Baby rabbits are gradually lost
    /// to obstacles and the farmer.
    /// </summary>
    public class BirthCarrot : PowerUpBase
    {
        [Header("Birth Carrot Settings")]
        [SerializeField] private GameObject babyRabbitPrefab;
        [SerializeField] private int babyCount = 3;

        private GameObject[] babyRabbits;

        protected override void Activate(Player.RabbitController rabbit)
        {
            isActive = true;
            // TODO: Spawn baby rabbits in adjacent lanes
            Debug.Log("Birth-Carrot activated! Baby rabbits spawned.");
        }

        protected override void Deactivate(Player.RabbitController rabbit)
        {
            isActive = false;
            Debug.Log("Birth-Carrot effect ended.");
        }
    }
}
