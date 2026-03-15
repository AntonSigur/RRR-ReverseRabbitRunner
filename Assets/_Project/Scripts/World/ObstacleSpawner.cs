using UnityEngine;

namespace ReverseRabbitRunner.World
{
    /// <summary>
    /// Spawns obstacles on the carrot farm lanes for the rabbit to dodge.
    /// </summary>
    public class ObstacleSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private GameObject[] obstaclePrefabs;
        [SerializeField] private float spawnInterval = 2f;
        [SerializeField] private float spawnDistance = 50f;

        [Header("Lane Settings")]
        [SerializeField] private float laneWidth = 3f;
        [SerializeField] private int laneCount = 5;

        [Header("Difficulty")]
        [SerializeField] private float minSpawnInterval = 0.5f;
        [SerializeField] private float difficultyIncreaseRate = 0.01f;

        [Header("References")]
        [SerializeField] private Transform playerTransform;

        private float timer;

        private void Update()
        {
            if (playerTransform == null) return;
            if (Core.GameManager.Instance?.CurrentState != Core.GameManager.GameState.Playing) return;

            timer += Time.deltaTime;

            // Gradually increase difficulty
            spawnInterval = Mathf.Max(minSpawnInterval, spawnInterval - difficultyIncreaseRate * Time.deltaTime);

            if (timer >= spawnInterval)
            {
                SpawnObstacle();
                timer = 0f;
            }
        }

        private void SpawnObstacle()
        {
            if (obstaclePrefabs == null || obstaclePrefabs.Length == 0) return;

            int lane = Random.Range(0, laneCount);
            int centerLane = laneCount / 2;
            float xPos = (lane - centerLane) * laneWidth;
            float zPos = playerTransform.position.z - spawnDistance;

            GameObject prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
            Instantiate(prefab, new Vector3(xPos, 0, zPos), Quaternion.identity, transform);
        }
    }
}
