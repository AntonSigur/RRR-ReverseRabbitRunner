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

        [Header("Obstacle Settings")]
        [SerializeField] private int obstacleStartChunk = 3;
        [SerializeField] private int baseObstaclesPerChunk = 2;
        [SerializeField] private int maxObstaclesPerChunk = 8;
        [SerializeField] private float obstacleRampRate = 0.5f;
        [SerializeField] private float minObstacleCarrotSpacing = 2f;

        [Header("Platform Settings")]
        [SerializeField] private int platformStartChunk = 5;
        [SerializeField] private float platformChance = 0.35f;
        [SerializeField] private float platformHeight = 1.0f;
        [SerializeField] private int platformMinLength = 8;
        [SerializeField] private int platformMaxLength = 12;

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
        private Material obstacleCrateMat;
        private Material obstacleHayMat;
        private Material obstacleFencePostMat;
        private Material obstacleScarecrowBodyMat;
        private Material obstacleScarecrowHatMat;
        private Material platformBedMat;
        private Material platformFrameMat;
        private Material platformWheelMat;
        private Material platformCabMat;

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

            // Obstacles (after warm-up chunks)
            SpawnObstaclesInChunk(chunkRoot.transform, chunkStartZ, themeIndex - 1);

            // Platform bonus (tractor flatbed with carrot jackpot)
            SpawnPlatformInChunk(chunkRoot.transform, chunkStartZ, themeIndex - 1);

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

        private void SpawnObstaclesInChunk(Transform parent, float chunkStartZ, int chunkIndex)
        {
            if (chunkIndex < obstacleStartChunk) return;

            // Lazy-init obstacle materials
            if (obstacleCrateMat == null)
            {
                obstacleCrateMat = MakeMat(new Color(0.45f, 0.28f, 0.12f));
                obstacleHayMat = MakeMat(new Color(0.85f, 0.75f, 0.35f));
                obstacleFencePostMat = MakeMat(new Color(0.35f, 0.22f, 0.10f));
                obstacleScarecrowBodyMat = MakeMat(new Color(0.50f, 0.35f, 0.15f));
                obstacleScarecrowHatMat = MakeMat(new Color(0.15f, 0.10f, 0.05f));
            }

            int obstacleCount = Mathf.Min(maxObstaclesPerChunk,
                Mathf.RoundToInt(baseObstaclesPerChunk + (chunkIndex - obstacleStartChunk) * obstacleRampRate));

            float tallRatio = Mathf.Clamp01((chunkIndex - obstacleStartChunk) * 0.05f);

            // Collect carrot Z positions per lane for overlap avoidance
            var carrotsByLane = new List<float>[maxLanes];
            for (int i = 0; i < maxLanes; i++) carrotsByLane[i] = new List<float>();
            foreach (Transform child in parent)
            {
                if (child.CompareTag("Carrot"))
                {
                    int lane = Mathf.RoundToInt(child.position.x / laneWidth) + maxLanes / 2;
                    lane = Mathf.Clamp(lane, 0, maxLanes - 1);
                    carrotsByLane[lane].Add(child.position.z);
                }
            }

            var placed = new List<(int lane, float z)>();
            float zRowTolerance = 2.0f;

            for (int i = 0; i < obstacleCount; i++)
            {
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    int lane = Random.Range(0, maxLanes);
                    float z = chunkStartZ - Random.Range(3f, chunkLength - 3f);

                    // Avoid overlapping carrots
                    bool carrotConflict = false;
                    foreach (float cz in carrotsByLane[lane])
                    {
                        if (Mathf.Abs(cz - z) < minObstacleCarrotSpacing)
                        {
                            carrotConflict = true;
                            break;
                        }
                    }
                    if (carrotConflict) continue;

                    // Guarantee at least 1 free lane at this Z row
                    var lanesNearZ = new HashSet<int>();
                    foreach (var (pl, pz) in placed)
                    {
                        if (Mathf.Abs(pz - z) < zRowTolerance)
                            lanesNearZ.Add(pl);
                    }
                    lanesNearZ.Add(lane);
                    if (lanesNearZ.Count >= maxLanes) continue;

                    // Avoid stacking obstacles in the same lane too close
                    bool tooClose = false;
                    foreach (var (pl, pz) in placed)
                    {
                        if (pl == lane && Mathf.Abs(pz - z) < 1.5f)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;

                    placed.Add((lane, z));

                    bool isTall = Random.value < tallRatio;
                    float xPos = (lane - maxLanes / 2) * laneWidth;
                    Vector3 pos = new Vector3(xPos, 0f, z);

                    if (isTall)
                    {
                        if (Random.value < 0.5f)
                            CreateFencePost(parent, pos);
                        else
                            CreateScarecrow(parent, pos);
                    }
                    else
                    {
                        if (Random.value < 0.5f)
                            CreateFarmCrate(parent, pos);
                        else
                            CreateHayBale(parent, pos);
                    }
                    break;
                }
            }
        }

        private void CreateFarmCrate(Transform parent, Vector3 position)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "FarmCrate";
            obj.tag = "Obstacle";
            obj.transform.parent = parent;
            obj.transform.position = position + new Vector3(0, 0.45f, 0);
            obj.transform.localScale = new Vector3(1.4f, 0.9f, 1.4f);
            obj.GetComponent<Renderer>().material = obstacleCrateMat;
            obj.GetComponent<Collider>().isTrigger = true;
        }

        private void CreateHayBale(Transform parent, Vector3 position)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.name = "HayBale";
            obj.tag = "Obstacle";
            obj.transform.parent = parent;
            obj.transform.position = position + new Vector3(0, 0.45f, 0);
            obj.transform.localScale = new Vector3(1.2f, 0.45f, 1.2f);
            obj.GetComponent<Renderer>().material = obstacleHayMat;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
            var col = obj.AddComponent<BoxCollider>();
            col.isTrigger = true;
        }

        private void CreateFencePost(Transform parent, Vector3 position)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.name = "FencePost";
            obj.tag = "Obstacle";
            obj.transform.parent = parent;
            obj.transform.position = position + new Vector3(0, 1.0f, 0);
            obj.transform.localScale = new Vector3(0.3f, 1.0f, 0.3f);
            obj.GetComponent<Renderer>().material = obstacleFencePostMat;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
            var col = obj.AddComponent<BoxCollider>();
            col.size = new Vector3(1f, 2f, 1f);
            col.isTrigger = true;
        }

        private void CreateScarecrow(Transform parent, Vector3 position)
        {
            var root = new GameObject("Scarecrow");
            root.tag = "Obstacle";
            root.transform.parent = parent;
            root.transform.position = position;

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pole.name = "Pole";
            pole.transform.parent = root.transform;
            pole.transform.localPosition = new Vector3(0, 0.9f, 0);
            pole.transform.localScale = new Vector3(0.15f, 1.8f, 0.15f);
            Object.DestroyImmediate(pole.GetComponent<Collider>());
            pole.GetComponent<Renderer>().material = obstacleScarecrowBodyMat;

            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "Arm";
            arm.transform.parent = root.transform;
            arm.transform.localPosition = new Vector3(0, 1.4f, 0);
            arm.transform.localScale = new Vector3(1.2f, 0.1f, 0.1f);
            Object.DestroyImmediate(arm.GetComponent<Collider>());
            arm.GetComponent<Renderer>().material = obstacleScarecrowBodyMat;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.parent = root.transform;
            head.transform.localPosition = new Vector3(0, 1.65f, 0);
            head.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().material = obstacleScarecrowBodyMat;

            var hat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hat.name = "Hat";
            hat.transform.parent = root.transform;
            hat.transform.localPosition = new Vector3(0, 1.9f, 0);
            hat.transform.localScale = new Vector3(0.5f, 0.08f, 0.5f);
            Object.DestroyImmediate(hat.GetComponent<Collider>());
            hat.GetComponent<Renderer>().material = obstacleScarecrowHatMat;

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.9f, 0);
            col.size = new Vector3(1.2f, 1.8f, 0.4f);
            col.isTrigger = true;
        }

        // ── Platform (Tractor Flatbed) ──────────────────────────────

        private void SpawnPlatformInChunk(Transform parent, float chunkStartZ, int chunkIndex)
        {
            if (chunkIndex < platformStartChunk) return;
            if (Random.value > platformChance) return;

            if (platformBedMat == null)
            {
                platformBedMat = MakeMat(new Color(0.35f, 0.35f, 0.38f));
                platformFrameMat = MakeMat(new Color(0.6f, 0.15f, 0.1f));
                platformWheelMat = MakeMat(new Color(0.1f, 0.1f, 0.1f));
                platformCabMat = MakeMat(new Color(0.2f, 0.45f, 0.15f));
            }

            int lane = Random.Range(0, maxLanes);
            float xPos = (lane - maxLanes / 2) * laneWidth;
            int length = Random.Range(platformMinLength, platformMaxLength + 1);

            float zStart = chunkStartZ - chunkLength * 0.3f;
            Vector3 pos = new Vector3(xPos, 0f, zStart);

            CreateTractorFlatbed(parent, pos, length);

            // Remove ALL ground-level carrots in the platform zone (all lanes)
            float zEnd = zStart - length;
            var toRemove = new List<Transform>();
            foreach (Transform child in parent)
            {
                if (child.CompareTag("Carrot") &&
                    child.position.z <= zStart + 1f && child.position.z >= zEnd - 1f &&
                    child.position.y < platformHeight)
                {
                    toRemove.Add(child);
                }
            }
            foreach (var t in toRemove) Object.DestroyImmediate(t.gameObject);
        }

        private void CreateTractorFlatbed(Transform parent, Vector3 position, int length)
        {
            var root = new GameObject("TractorFlatbed");
            root.transform.parent = parent;
            root.transform.position = position;

            float bedWidth = 2.8f;
            float bedThick = 0.15f;
            float bedY = platformHeight;
            float halfLen = length * 0.5f;

            // Bed — solid collider, rabbit walks on this
            var bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bed.name = "Bed";
            bed.transform.parent = root.transform;
            bed.transform.localPosition = new Vector3(0, bedY, -halfLen);
            bed.transform.localScale = new Vector3(bedWidth, bedThick, length);
            bed.GetComponent<Renderer>().material = platformBedMat;

            // Frame under the bed
            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Frame";
            frame.transform.parent = root.transform;
            frame.transform.localPosition = new Vector3(0, bedY * 0.45f, -halfLen);
            frame.transform.localScale = new Vector3(bedWidth - 0.4f, bedY * 0.75f, length - 0.6f);
            Object.DestroyImmediate(frame.GetComponent<Collider>());
            frame.GetComponent<Renderer>().material = platformFrameMat;

            // 4 wheels at corners
            float wheelR = 0.4f;
            float wheelT = 0.2f;
            float wxOff = bedWidth * 0.5f - 0.1f;
            Vector3[] wp = {
                new( wxOff, wheelR, -0.5f),          new(-wxOff, wheelR, -0.5f),
                new( wxOff, wheelR, -(length-0.5f)),  new(-wxOff, wheelR, -(length-0.5f))
            };
            for (int i = 0; i < 4; i++)
            {
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = $"Wheel_{i}";
                wheel.transform.parent = root.transform;
                wheel.transform.localPosition = wp[i];
                wheel.transform.localScale = new Vector3(wheelR * 2, wheelT, wheelR * 2);
                wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
                Object.DestroyImmediate(wheel.GetComponent<Collider>());
                wheel.GetComponent<Renderer>().material = platformWheelMat;
            }

            // Cab at far end (-Z)
            float cabH = 1.0f;
            float cabD = 1.5f;
            var cab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cab.name = "Cab";
            cab.transform.parent = root.transform;
            cab.transform.localPosition = new Vector3(0, bedY + cabH * 0.5f + bedThick * 0.5f,
                -length + cabD * 0.5f);
            cab.transform.localScale = new Vector3(bedWidth * 0.8f, cabH, cabD);
            Object.DestroyImmediate(cab.GetComponent<Collider>());
            cab.GetComponent<Renderer>().material = platformCabMat;

            // Exhaust pipe
            var exhaust = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            exhaust.name = "Exhaust";
            exhaust.transform.parent = root.transform;
            exhaust.transform.localPosition = new Vector3(bedWidth * 0.3f,
                bedY + cabH + 0.3f, -length + cabD * 0.3f);
            exhaust.transform.localScale = new Vector3(0.12f, 0.3f, 0.12f);
            Object.DestroyImmediate(exhaust.GetComponent<Collider>());
            exhaust.GetComponent<Renderer>().material = platformWheelMat;

            // Front bumper trigger — stumble if rabbit walks into it at ground level
            var bumper = new GameObject("FrontBumper");
            bumper.tag = "Obstacle";
            bumper.transform.parent = root.transform;
            bumper.transform.localPosition = Vector3.zero;
            var bumperCol = bumper.AddComponent<BoxCollider>();
            bumperCol.center = new Vector3(0, bedY * 0.5f, 0.3f);
            bumperCol.size = new Vector3(bedWidth, bedY, 0.5f);
            bumperCol.isTrigger = true;

            // ── Packed carrot cargo! 🥕🥕🥕 ──
            // Dense rows filling the entire flatbed — the wagon IS the carrot source
            Material carrotMat = MakeMat(new Color(1f, 0.5f, 0.05f));
            Material leavesMat = MakeMat(new Color(0.1f, 0.6f, 0.1f));
            float carrotY = bedY + bedThick * 0.5f + 0.44f;
            float usableLen = length - cabD - 1.0f;
            float carrotSpaceZ = 1.0f;   // one carrot every 1 unit along length
            float[] xOffsets = { -0.8f, 0f, 0.8f }; // 3 columns across the bed

            for (float z = -0.8f; z > -usableLen; z -= carrotSpaceZ)
            {
                foreach (float xOff in xOffsets)
                {
                    var carrot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    carrot.name = "Carrot";
                    carrot.tag = "Carrot";
                    carrot.transform.parent = parent;
                    carrot.transform.position = position + new Vector3(xOff, carrotY, z);
                    carrot.transform.localScale = new Vector3(0.34f, 0.68f, 0.34f);
                    carrot.transform.rotation = Quaternion.Euler(0, 0, 180f);

                    var leaves = GameObject.CreatePrimitive(PrimitiveType.Cube);
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
