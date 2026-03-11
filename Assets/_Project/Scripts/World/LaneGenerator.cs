using UnityEngine;
using System.Collections.Generic;

namespace ReverseRabbitRunner.World
{
    /// <summary>
    /// Generates and recycles lane segments as the rabbit runs.
    /// Implements infinite scrolling terrain using object pooling.
    /// </summary>
    public class LaneGenerator : MonoBehaviour
    {
        [Header("Segment Settings")]
        [SerializeField] private GameObject[] segmentPrefabs;
        [SerializeField] private float segmentLength = 20f;
        [SerializeField] private int initialSegments = 5;
        [SerializeField] private int bufferSegments = 3;

        [Header("References")]
        [SerializeField] private Transform playerTransform;

        private readonly List<GameObject> activeSegments = new();
        private float spawnZ;
        private float recycleZ;

        private void Start()
        {
            spawnZ = 0f;

            for (int i = 0; i < initialSegments; i++)
            {
                SpawnSegment();
            }
        }

        private void Update()
        {
            if (playerTransform == null) return;

            // Spawn new segments ahead of the player (behind the rabbit, since it runs backwards)
            while (spawnZ > playerTransform.position.z - (initialSegments * segmentLength))
            {
                SpawnSegment();
            }

            // Recycle segments the player has passed
            while (activeSegments.Count > 0 &&
                   activeSegments[0].transform.position.z > playerTransform.position.z + segmentLength * 2)
            {
                RecycleSegment();
            }
        }

        private void SpawnSegment()
        {
            if (segmentPrefabs == null || segmentPrefabs.Length == 0) return;

            GameObject prefab = segmentPrefabs[Random.Range(0, segmentPrefabs.Length)];
            GameObject segment = Instantiate(prefab, new Vector3(0, 0, spawnZ), Quaternion.identity, transform);
            activeSegments.Add(segment);
            spawnZ -= segmentLength;
        }

        private void RecycleSegment()
        {
            if (activeSegments.Count == 0) return;

            GameObject oldest = activeSegments[0];
            activeSegments.RemoveAt(0);
            Destroy(oldest);
        }
    }
}
