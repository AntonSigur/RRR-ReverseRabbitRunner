using UnityEngine;
using UnityEditor;

namespace ReverseRabbitRunner.Editor
{
    /// <summary>
    /// Editor utility to set up the main game scene with ground, lanes, lighting, and placeholder objects.
    /// Run from menu: ReverseRabbitRunner > Setup Game Scene
    /// </summary>
    public static class SceneSetup
    {
        private const float LaneWidth = 3f;
        private const int MaxLanes = 5;
        private const float SegmentLength = 40f;
        private const float GroundWidth = MaxLanes * LaneWidth + 4f;

        [MenuItem("ReverseRabbitRunner/Setup Game Scene")]
        public static void SetupGameScene()
        {
            CleanupScene();
            SetupLighting();
            CreateGround();
            CreateLaneMarkers();
            GameObject player = CreatePlayerPlaceholder();
            CreateFarmerPlaceholder(player.transform);
            CreateManagers();
            CreateSampleCarrots();
            SetupEnvironmentSettings();

            Debug.Log("<b><color=#4CAF50>ReverseRabbitRunner</color></b>: Game scene setup complete!");

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }

        private static void CleanupScene()
        {
            var mainCam = GameObject.Find("Main Camera");
            if (mainCam != null) Object.DestroyImmediate(mainCam);

            var dirLight = GameObject.Find("Directional Light");
            if (dirLight != null) Object.DestroyImmediate(dirLight);

            string[] toRemove = { "[Ground]", "[Lanes]", "[Player]", "[Farmer]", "[Managers]",
                                   "[Environment]", "[Cameras]", "[Sun]", "[Carrots]" };
            foreach (var name in toRemove)
            {
                var obj = GameObject.Find(name);
                if (obj != null) Object.DestroyImmediate(obj);
            }
        }

        private static void SetupLighting()
        {
            GameObject sunObj = new GameObject("[Sun]");
            Light sun = sunObj.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sunObj.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        private static GameObject CreateGround()
        {
            GameObject groundParent = new GameObject("[Ground]");

            // Farm ground
            GameObject farmGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
            farmGround.name = "FarmGround";
            farmGround.transform.parent = groundParent.transform;
            farmGround.transform.localScale = new Vector3(GroundWidth, 0.1f, SegmentLength * 5);
            farmGround.transform.position = new Vector3(0, -0.05f, -SegmentLength * 2);

            var groundRenderer = farmGround.GetComponent<Renderer>();
            Material groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMat.color = new Color(0.45f, 0.30f, 0.15f);
            groundMat.name = "FarmGround_Mat";
            groundRenderer.material = groundMat;

            // Running path — slightly lighter
            GameObject path = GameObject.CreatePrimitive(PrimitiveType.Cube);
            path.name = "RunningPath";
            path.transform.parent = groundParent.transform;
            path.transform.localScale = new Vector3(MaxLanes * LaneWidth, 0.12f, SegmentLength * 5);
            path.transform.position = new Vector3(0, -0.04f, -SegmentLength * 2);

            var pathRenderer = path.GetComponent<Renderer>();
            Material pathMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            pathMat.color = new Color(0.55f, 0.40f, 0.25f);
            pathMat.name = "DirtPath_Mat";
            pathRenderer.material = pathMat;

            // Green carrot field strips on sides
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject field = GameObject.CreatePrimitive(PrimitiveType.Cube);
                field.name = side < 0 ? "CarrotField_Left" : "CarrotField_Right";
                field.transform.parent = groundParent.transform;
                field.transform.localScale = new Vector3(8f, 0.15f, SegmentLength * 5);
                float xPos = side * (GroundWidth / 2f + 2f);
                field.transform.position = new Vector3(xPos, -0.025f, -SegmentLength * 2);

                var fieldRenderer = field.GetComponent<Renderer>();
                Material fieldMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                fieldMat.color = new Color(0.2f, 0.55f, 0.15f);
                fieldMat.name = $"CarrotField_{(side < 0 ? "Left" : "Right")}_Mat";
                fieldRenderer.material = fieldMat;
            }

            return groundParent;
        }

        private static GameObject CreateLaneMarkers()
        {
            GameObject laneParent = new GameObject("[Lanes]");

            for (int i = 0; i <= MaxLanes; i++)
            {
                float xPos = (i - MaxLanes / 2f) * LaneWidth;

                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = $"LaneDivider_{i}";
                marker.transform.parent = laneParent.transform;
                marker.transform.localScale = new Vector3(0.05f, 0.02f, SegmentLength * 5);
                marker.transform.position = new Vector3(xPos, 0.01f, -SegmentLength * 2);

                Object.DestroyImmediate(marker.GetComponent<Collider>());

                var renderer = marker.GetComponent<Renderer>();
                Material lineMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                lineMat.color = new Color(0.7f, 0.55f, 0.35f, 0.5f);
                renderer.material = lineMat;
            }

            return laneParent;
        }

        private static GameObject CreatePlayerPlaceholder()
        {
            GameObject playerParent = new GameObject("[Player]");
            playerParent.tag = "Player";
            playerParent.transform.position = new Vector3(0, 1f, 0);

            // Rabbit body — positioned so bottom is at the parent's origin
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "RabbitBody";
            body.transform.parent = playerParent.transform;
            body.transform.localPosition = new Vector3(0, 0f, 0);
            body.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
            // Remove capsule's own collider (CharacterController handles collision)
            Object.DestroyImmediate(body.GetComponent<Collider>());

            var bodyRenderer = body.GetComponent<Renderer>();
            Material rabbitMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            rabbitMat.color = new Color(0.9f, 0.9f, 0.9f);
            rabbitMat.name = "Rabbit_Mat";
            bodyRenderer.material = rabbitMat;

            // Ears with mirrors
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject ear = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ear.name = side < 0 ? "LeftEar" : "RightEar";
                ear.transform.parent = playerParent.transform;
                ear.transform.localPosition = new Vector3(side * 0.15f, 0.7f, 0);
                ear.transform.localScale = new Vector3(0.08f, 0.4f, 0.05f);
                ear.transform.localRotation = Quaternion.Euler(0, 0, side * -10f);
                Object.DestroyImmediate(ear.GetComponent<Collider>());

                var earRenderer = ear.GetComponent<Renderer>();
                Material earMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                earMat.color = new Color(1f, 0.85f, 0.85f);
                earRenderer.material = earMat;

                // Mirror on ear
                GameObject mirror = GameObject.CreatePrimitive(PrimitiveType.Quad);
                mirror.name = side < 0 ? "LeftMirror" : "RightMirror";
                mirror.transform.parent = ear.transform;
                mirror.transform.localPosition = new Vector3(side * 0.6f, 0.3f, 0);
                mirror.transform.localScale = new Vector3(0.8f, 0.5f, 1f);
                mirror.transform.localRotation = Quaternion.Euler(0, side * 45f, 0);
                Object.DestroyImmediate(mirror.GetComponent<Collider>());

                var mirrorRenderer = mirror.GetComponent<Renderer>();
                Material mirrorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mirrorMat.color = new Color(0.7f, 0.85f, 1f);
                mirrorMat.SetFloat("_Smoothness", 0.95f);
                mirrorMat.SetFloat("_Metallic", 0.8f);
                mirrorMat.name = $"{mirror.name}_Mat";
                mirrorRenderer.material = mirrorMat;
            }

            // CharacterController — skinWidth keeps rabbit above ground
            CharacterController cc = playerParent.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 0f, 0);
            cc.radius = 0.3f;
            cc.height = 1f;
            cc.skinWidth = 0.08f;

            // RabbitController
            playerParent.AddComponent<Player.RabbitController>();

            // Face backwards
            playerParent.transform.rotation = Quaternion.Euler(0, 180f, 0);

            // --- Main Camera ---
            GameObject cameraParent = new GameObject("[Cameras]");
            GameObject mainCamObj = new GameObject("MainCamera");
            mainCamObj.tag = "MainCamera";
            mainCamObj.transform.parent = cameraParent.transform;
            mainCamObj.transform.position = new Vector3(0, 3.5f, -10f);
            mainCamObj.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
            Camera mainCam = mainCamObj.AddComponent<Camera>();
            mainCam.fieldOfView = 60f;
            mainCam.nearClipPlane = 0.3f;
            mainCam.farClipPlane = 200f;
            mainCamObj.AddComponent<AudioListener>();
            mainCamObj.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

            // CameraFollow for smooth tracking
            var camFollow = mainCamObj.AddComponent<Player.CameraFollow>();

            return playerParent;
        }

        private static GameObject CreateFarmerPlaceholder(Transform playerTransform)
        {
            GameObject farmerParent = new GameObject("[Farmer]");
            farmerParent.tag = "Farmer";
            farmerParent.transform.position = new Vector3(0, 0, 15f);

            // Try to load the Adventure Character prefab
            GameObject farmerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Adventure_Character/Prefabs/Man_01.prefab");

            if (farmerPrefab != null)
            {
                GameObject farmerModel = (GameObject)PrefabUtility.InstantiatePrefab(farmerPrefab);
                farmerModel.name = "FarmerModel";
                farmerModel.transform.parent = farmerParent.transform;
                farmerModel.transform.localPosition = Vector3.zero;
                farmerModel.transform.localScale = Vector3.one * 2.3f;
                // Face towards the rabbit (which is at -Z from the farmer)
                farmerModel.transform.localRotation = Quaternion.identity;

                // Fix pink materials — upgrade to URP Lit shader
                UpgradeMaterialsToURP(farmerModel);
            }
            else
            {
                // Fallback: primitive placeholder
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "FarmerBody";
                body.transform.parent = farmerParent.transform;
                body.transform.localPosition = new Vector3(0, 1f, 0);
                body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);

                var bodyRenderer = body.GetComponent<Renderer>();
                Material farmerMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                farmerMat.color = new Color(0.6f, 0.3f, 0.15f);
                farmerMat.name = "Farmer_Mat";
                bodyRenderer.material = farmerMat;

                Debug.LogWarning("Adventure Character prefab not found at Assets/Adventure_Character/Prefabs/Man_01.prefab — using placeholder");
            }

            farmerParent.AddComponent<Enemies.FarmerController>();

            return farmerParent;
        }

        private static GameObject CreateManagers()
        {
            GameObject managers = new GameObject("[Managers]");

            GameObject gmObj = new GameObject("GameManager");
            gmObj.transform.parent = managers.transform;
            gmObj.AddComponent<Core.GameManager>();

            GameObject smObj = new GameObject("ScoreManager");
            smObj.transform.parent = managers.transform;
            smObj.AddComponent<Core.ScoreManager>();

            GameObject imObj = new GameObject("InputManager");
            imObj.transform.parent = managers.transform;
            imObj.AddComponent<Core.InputManager>();

            GameObject lgObj = new GameObject("LaneGenerator");
            lgObj.transform.parent = managers.transform;
            lgObj.AddComponent<World.LaneGenerator>();

            GameObject osObj = new GameObject("ObstacleSpawner");
            osObj.transform.parent = managers.transform;
            osObj.AddComponent<World.ObstacleSpawner>();

            // HUD
            GameObject hudObj = new GameObject("GameHUD");
            hudObj.transform.parent = managers.transform;
            hudObj.AddComponent<UI.GameHUD>();

            // Auto-start helper
            GameObject autoStart = new GameObject("AutoStart");
            autoStart.transform.parent = managers.transform;
            autoStart.AddComponent<Core.AutoStartGame>();

            return managers;
        }

        private static void CreateSampleCarrots()
        {
            GameObject carrotParent = new GameObject("[Carrots]");

            // Place lots of carrots along the path
            for (int i = 0; i < 60; i++)
            {
                int lane = Random.Range(0, MaxLanes);
                float xPos = (lane - MaxLanes / 2) * LaneWidth;
                float zPos = -(i * 4f + 8f);

                GameObject carrot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                carrot.name = $"Carrot_{i}";
                carrot.tag = "Carrot";
                carrot.transform.parent = carrotParent.transform;
                carrot.transform.position = new Vector3(xPos, 0.3f, zPos);
                carrot.transform.localScale = new Vector3(0.15f, 0.3f, 0.15f);
                carrot.transform.rotation = Quaternion.Euler(0, 0, 180f);// pointy end up

                // Carrot top (green leaves)
                GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leaves.name = "CarrotLeaves";
                leaves.transform.parent = carrot.transform;
                leaves.transform.localPosition = new Vector3(0, -0.7f, 0);
                leaves.transform.localScale = new Vector3(2f, 0.3f, 2f);
                Object.DestroyImmediate(leaves.GetComponent<Collider>());

                var leavesRenderer = leaves.GetComponent<Renderer>();
                Material leavesMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                leavesMat.color = new Color(0.1f, 0.6f, 0.1f);
                leavesRenderer.material = leavesMat;

                // Orange carrot body
                var carrotRenderer = carrot.GetComponent<Renderer>();
                Material carrotMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                carrotMat.color = new Color(1f, 0.5f, 0.05f); // bright orange
                carrotMat.name = "Carrot_Mat";
                carrotRenderer.material = carrotMat;

                // Make it a trigger
                carrot.GetComponent<Collider>().isTrigger = true;
            }
        }

        private static void SetupEnvironmentSettings()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.75f, 0.9f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.75f, 0.85f, 0.95f);
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 120f;
        }

        /// <summary>
        /// Upgrades all materials on a GameObject to URP Lit shader,
        /// preserving their albedo textures.
        /// </summary>
        private static void UpgradeMaterialsToURP(GameObject obj)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogWarning("URP Lit shader not found — cannot upgrade materials");
                return;
            }

            var renderers = obj.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (mats[i].shader == urpLit) continue; // already URP

                    // Create a new URP material preserving textures
                    Material newMat = new Material(urpLit);
                    newMat.name = mats[i].name + "_URP";

                    // Copy albedo texture if present
                    if (mats[i].HasProperty("_MainTex"))
                    {
                        Texture mainTex = mats[i].GetTexture("_MainTex");
                        if (mainTex != null)
                            newMat.SetTexture("_BaseMap", mainTex);
                    }

                    // Copy color
                    if (mats[i].HasProperty("_Color"))
                        newMat.SetColor("_BaseColor", mats[i].GetColor("_Color"));

                    // Copy normal map if present
                    if (mats[i].HasProperty("_BumpMap"))
                    {
                        Texture normalTex = mats[i].GetTexture("_BumpMap");
                        if (normalTex != null)
                        {
                            newMat.SetTexture("_BumpMap", normalTex);
                            newMat.EnableKeyword("_NORMALMAP");
                        }
                    }

                    // Copy metallic/smoothness
                    if (mats[i].HasProperty("_MetallicGlossMap"))
                    {
                        Texture metalTex = mats[i].GetTexture("_MetallicGlossMap");
                        if (metalTex != null)
                            newMat.SetTexture("_MetallicGlossMap", metalTex);
                    }

                    if (mats[i].HasProperty("_OcclusionMap"))
                    {
                        Texture aoTex = mats[i].GetTexture("_OcclusionMap");
                        if (aoTex != null)
                            newMat.SetTexture("_OcclusionMap", aoTex);
                    }

                    mats[i] = newMat;
                }
                renderer.sharedMaterials = mats;
            }
        }
    }
}
