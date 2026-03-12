using UnityEngine;
using System.Collections.Generic;

namespace ReverseRabbitRunner.World
{
    /// <summary>
    /// Procedural infinite world: spawns themed chunks ahead of the player,
    /// despawns behind, and performs origin shifting every 1km.
    /// </summary>
    public class ChunkManager : MonoBehaviour
    {
        [Header("Chunk Settings")]
        [SerializeField] private float chunkLength = 50f;
        [SerializeField] private int chunksAhead = 3;
        [SerializeField] private int chunksBehind = 1;

        [Header("Origin Shifting")]
        [SerializeField] private float originShiftThreshold = 250f;

        [Header("Lane Settings")]
        [SerializeField] private int maxLanes = 5;
        [SerializeField] private float laneWidth = 3f;

        [Header("Carrot Settings")]
        [SerializeField] private int carrotsPerChunk = 15;
        [SerializeField] private float carrotSpacing = 3f;

        // Public stats for HUD
        public float TotalDistance { get; private set; }
        public int CurrentChunkIndex { get; private set; }
        public int OriginShiftCount { get; private set; }
        public int ActiveChunkCount => activeChunks.Count;

        private Transform playerTransform;
        private readonly List<ChunkData> activeChunks = new();
        private float nextSpawnZ;
        private float totalShifted;
        private Shader urpLit;
        private float lastShiftTime;

        // Theme definitions
        public enum ChunkTheme { Concrete, SnowMud, Grass }
        private int themeIndex = 0;

        private struct ChunkData
        {
            public GameObject root;
            public float startZ; // world Z of chunk's positive edge
            public int index;
            public ChunkTheme theme;
        }

        private struct ThemeColors
        {
            public Color ground;
            public Color path;
            public Color laneLines;
            public Color sideField;
            public string label;
        }

        private void Start()
        {
            urpLit = Shader.Find("Universal Render Pipeline/Lit");

            // Destroy editor preview ground (chunks replace it)
            var previewGround = GameObject.Find("[Ground]");
            if (previewGround != null) Destroy(previewGround);

            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;

            if (playerTransform == null)
            {
                Debug.LogError("ChunkManager: No Player found!");
                return;
            }

            // Start spawning from behind the player (+Z) to ahead (-Z)
            nextSpawnZ = chunkLength; // one chunk behind start

            // Spawn initial chunks
            for (int i = 0; i < chunksAhead + chunksBehind + 1; i++)
            {
                SpawnNextChunk();
            }
        }

        private void Update()
        {
            if (playerTransform == null) return;

            float playerZ = playerTransform.position.z;

            // Update total distance (player moves in -Z)
            TotalDistance = -playerZ + totalShifted;

            // Current chunk index based on distance
            CurrentChunkIndex = Mathf.FloorToInt(TotalDistance / chunkLength);

            // Spawn chunks ahead
            while (nextSpawnZ > playerZ - chunksAhead * chunkLength)
            {
                SpawnNextChunk();
            }

            // Despawn chunks far behind (extra buffer so they don't pop out visibly)
            while (activeChunks.Count > 0 &&
                   activeChunks[0].startZ > playerZ + (chunksBehind + 1) * chunkLength + chunkLength * 2)
            {
                DespawnOldestChunk();
            }

            // Origin shifting — only when far from origin, with cooldown
            if (Mathf.Abs(playerZ) > originShiftThreshold && Time.time - lastShiftTime > 1f)
            {
                PerformOriginShift();
            }
        }

        private void SpawnNextChunk()
        {
            ChunkTheme theme = (ChunkTheme)(themeIndex % 3);
            themeIndex++;

            float chunkStartZ = nextSpawnZ;
            float chunkCenterZ = chunkStartZ - chunkLength / 2f;

            GameObject chunkRoot = new GameObject($"Chunk_{themeIndex - 1}_{theme}");
            chunkRoot.transform.parent = transform;

            ThemeColors colors = GetThemeColors(theme);
            float groundWidth = maxLanes * laneWidth + 4f;

            // Ground plane
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.parent = chunkRoot.transform;
            ground.transform.localScale = new Vector3(groundWidth + 50f, 0.1f, chunkLength);
            ground.transform.position = new Vector3(0, -0.05f, chunkCenterZ);
            ground.GetComponent<Renderer>().material = MakeMat(colors.ground);

            // Running path (on top of ground)
            GameObject path = GameObject.CreatePrimitive(PrimitiveType.Cube);
            path.name = "RunningPath";
            path.transform.parent = chunkRoot.transform;
            path.transform.localScale = new Vector3(maxLanes * laneWidth, 0.12f, chunkLength);
            path.transform.position = new Vector3(0, -0.04f, chunkCenterZ);
            path.GetComponent<Renderer>().material = MakeMat(colors.path);

            // Lane dividers
            for (int i = 0; i <= maxLanes; i++)
            {
                float xPos = (i - maxLanes / 2f) * laneWidth;
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = $"Lane_{i}";
                marker.transform.parent = chunkRoot.transform;
                marker.transform.localScale = new Vector3(0.06f, 0.02f, chunkLength);
                marker.transform.position = new Vector3(xPos, 0.01f, chunkCenterZ);
                Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.GetComponent<Renderer>().material = MakeMat(colors.laneLines);
            }

            // Side fields
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject field = GameObject.CreatePrimitive(PrimitiveType.Cube);
                field.name = side < 0 ? "Field_Left" : "Field_Right";
                field.transform.parent = chunkRoot.transform;
                field.transform.localScale = new Vector3(24f, 0.15f, chunkLength);
                float xPos = side * (groundWidth / 2f + 12f);
                field.transform.position = new Vector3(xPos, -0.025f, chunkCenterZ);
                field.GetComponent<Renderer>().material = MakeMat(colors.sideField);
            }

            // Carrots scattered in the chunk
            SpawnCarrotsInChunk(chunkRoot.transform, chunkStartZ);

            activeChunks.Add(new ChunkData
            {
                root = chunkRoot,
                startZ = chunkStartZ,
                index = themeIndex - 1,
                theme = theme
            });

            nextSpawnZ -= chunkLength;
        }

        private void SpawnCarrotsInChunk(Transform parent, float chunkStartZ)
        {
            Material carrotMat = MakeMat(new Color(1f, 0.5f, 0.05f));
            Material leavesMat = MakeMat(new Color(0.1f, 0.6f, 0.1f));

            for (int i = 0; i < carrotsPerChunk; i++)
            {
                int lane = Random.Range(0, maxLanes);
                float xPos = (lane - maxLanes / 2) * laneWidth;
                float zPos = chunkStartZ - Random.Range(2f, chunkLength - 2f);

                GameObject carrot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                carrot.name = $"Carrot";
                carrot.tag = "Carrot";
                carrot.transform.parent = parent;
                carrot.transform.position = new Vector3(xPos, 0.44f, zPos);
                carrot.transform.localScale = new Vector3(0.34f, 0.68f, 0.34f);
                carrot.transform.rotation = Quaternion.Euler(0, 0, 180f);

                // Leaves
                GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leaves.name = "Leaves";
                leaves.transform.parent = carrot.transform;
                leaves.transform.localPosition = new Vector3(0, -0.7f, 0);
                leaves.transform.localScale = new Vector3(2f, 0.3f, 2f);
                Object.DestroyImmediate(leaves.GetComponent<Collider>());
                leaves.GetComponent<Renderer>().material = leavesMat;

                carrot.GetComponent<Renderer>().material = carrotMat;
                carrot.GetComponent<Collider>().isTrigger = true;
                carrot.AddComponent<CarrotBob>();
            }
        }

        private void DespawnOldestChunk()
        {
            if (activeChunks.Count == 0) return;
            Destroy(activeChunks[0].root);
            activeChunks.RemoveAt(0);
        }

        private void PerformOriginShift()
        {
            Vector3 offset = new Vector3(0, 0, playerTransform.position.z);

            if (Mathf.Abs(offset.z) < 1f) return; // safety: skip tiny shifts

            totalShifted += Mathf.Abs(offset.z);
            OriginShiftCount++;
            lastShiftTime = Time.time;

            // Disable CharacterController before teleporting (Unity requires this)
            var cc = playerTransform.GetComponentInChildren<CharacterController>();
            if (cc != null) cc.enabled = false;

            // Shift player
            playerTransform.position -= offset;

            // Re-enable CharacterController
            if (cc != null) cc.enabled = true;

            // Shift camera (if not parented to player)
            var cam = Camera.main;
            if (cam != null && cam.transform.parent != playerTransform
                && !cam.transform.IsChildOf(playerTransform))
                cam.transform.position -= offset;

            // Shift farmer
            var farmerObj = GameObject.FindGameObjectWithTag("Farmer");
            if (farmerObj != null) farmerObj.transform.position -= offset;

            // Shift all chunks
            foreach (var chunk in activeChunks)
            {
                if (chunk.root != null)
                    chunk.root.transform.position -= offset;
            }

            // Update spawn position
            nextSpawnZ -= offset.z;

            // Update stored chunk startZ values
            for (int i = 0; i < activeChunks.Count; i++)
            {
                var c = activeChunks[i];
                c.startZ -= offset.z;
                activeChunks[i] = c;
            }

            Debug.Log($"[OriginShift #{OriginShiftCount}] Shifted {offset.z:F0}m | Player now at {playerTransform.position.z:F1} | Total: {totalShifted:F0}m");
        }

        private ThemeColors GetThemeColors(ChunkTheme theme)
        {
            switch (theme)
            {
                case ChunkTheme.Concrete:
                    return new ThemeColors
                    {
                        ground = new Color(0.5f, 0.5f, 0.5f),     // gray
                        path = new Color(0.65f, 0.65f, 0.65f),     // lighter gray
                        laneLines = new Color(1f, 1f, 1f, 0.9f),   // white lines
                        sideField = new Color(0.3f, 0.3f, 0.35f),  // dark gray sides
                        label = "Concrete"
                    };
                case ChunkTheme.SnowMud:
                    return new ThemeColors
                    {
                        ground = new Color(0.9f, 0.9f, 0.95f),     // snow white
                        path = new Color(0.4f, 0.28f, 0.15f),      // muddy brown
                        laneLines = new Color(0.25f, 0.2f, 0.15f),  // dark lines
                        sideField = new Color(0.85f, 0.85f, 0.9f),  // snowy sides
                        label = "Snow+Mud"
                    };
                case ChunkTheme.Grass:
                default:
                    return new ThemeColors
                    {
                        ground = new Color(0.45f, 0.30f, 0.15f),    // farm dirt
                        path = new Color(0.55f, 0.40f, 0.25f),      // dirt path
                        laneLines = new Color(0.7f, 0.55f, 0.35f),  // subtle lines
                        sideField = new Color(0.2f, 0.55f, 0.15f),  // green fields
                        label = "Grass"
                    };
            }
        }

        private Material MakeMat(Color color)
        {
            Material mat = new Material(urpLit);
            mat.color = color;
            return mat;
        }
    }
}
