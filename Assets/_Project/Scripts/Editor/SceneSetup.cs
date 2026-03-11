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

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");

            // Rabbit body — round and cute
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "RabbitBody";
            body.transform.parent = playerParent.transform;
            body.transform.localPosition = new Vector3(0, 0f, 0);
            body.transform.localScale = new Vector3(0.6f, 0.5f, 0.55f);
            Object.DestroyImmediate(body.GetComponent<Collider>());

            var bodyRenderer = body.GetComponent<Renderer>();
            Material rabbitMat = new Material(urpLit);
            rabbitMat.color = new Color(0.92f, 0.92f, 0.92f);
            rabbitMat.name = "Rabbit_Mat";
            bodyRenderer.material = rabbitMat;

            // Head (slightly larger sphere)
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "RabbitHead";
            head.transform.parent = playerParent.transform;
            head.transform.localPosition = new Vector3(0, 0.55f, 0);
            head.transform.localScale = new Vector3(0.5f, 0.45f, 0.45f);
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().material = rabbitMat;

            // Rabbit face — eyes
            for (int side = -1; side <= 1; side += 2)
            {
                // Eye whites
                GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = side < 0 ? "LeftEye" : "RightEye";
                eye.transform.parent = head.transform;
                eye.transform.localPosition = new Vector3(side * 0.25f, 0.1f, -0.8f);
                eye.transform.localScale = new Vector3(0.25f, 0.3f, 0.15f);
                Object.DestroyImmediate(eye.GetComponent<Collider>());

                var eyeRenderer = eye.GetComponent<Renderer>();
                Material eyeMat = new Material(urpLit);
                eyeMat.color = Color.white;
                eyeRenderer.material = eyeMat;

                // Pupils
                GameObject pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pupil.name = "Pupil";
                pupil.transform.parent = eye.transform;
                pupil.transform.localPosition = new Vector3(0, 0, -0.35f);
                pupil.transform.localScale = new Vector3(0.45f, 0.55f, 0.4f);
                Object.DestroyImmediate(pupil.GetComponent<Collider>());

                var pupilRenderer = pupil.GetComponent<Renderer>();
                Material pupilMat = new Material(urpLit);
                pupilMat.color = new Color(0.1f, 0.05f, 0.0f);
                pupilRenderer.material = pupilMat;
            }

            // Rabbit nose (pink sphere)
            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nose.name = "RabbitNose";
            nose.transform.parent = head.transform;
            nose.transform.localPosition = new Vector3(0, -0.1f, -0.9f);
            nose.transform.localScale = new Vector3(0.15f, 0.12f, 0.1f);
            Object.DestroyImmediate(nose.GetComponent<Collider>());

            var noseRenderer = nose.GetComponent<Renderer>();
            Material noseMat = new Material(urpLit);
            noseMat.color = new Color(1f, 0.5f, 0.6f);
            noseRenderer.material = noseMat;

            // Buck teeth
            GameObject teeth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            teeth.name = "BuckTeeth";
            teeth.transform.parent = head.transform;
            teeth.transform.localPosition = new Vector3(0, -0.3f, -0.85f);
            teeth.transform.localScale = new Vector3(0.15f, 0.15f, 0.05f);
            Object.DestroyImmediate(teeth.GetComponent<Collider>());

            var teethRenderer = teeth.GetComponent<Renderer>();
            Material teethMat = new Material(urpLit);
            teethMat.color = Color.white;
            teethRenderer.material = teethMat;

            // Fluffy tail
            GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tail.name = "RabbitTail";
            tail.transform.parent = playerParent.transform;
            tail.transform.localPosition = new Vector3(0, 0f, 0.4f);
            tail.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            Object.DestroyImmediate(tail.GetComponent<Collider>());
            tail.GetComponent<Renderer>().material = rabbitMat;

            // LARGE ears
            for (int side = -1; side <= 1; side += 2)
            {
                // Ear — tall, slightly tilted outward
                GameObject ear = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ear.name = side < 0 ? "LeftEar" : "RightEar";
                ear.transform.parent = playerParent.transform;
                ear.transform.localPosition = new Vector3(side * 0.18f, 0.95f, 0);
                ear.transform.localScale = new Vector3(0.12f, 0.55f, 0.08f);
                ear.transform.localRotation = Quaternion.Euler(0, 0, side * -12f);
                Object.DestroyImmediate(ear.GetComponent<Collider>());

                // Pink inner ear
                Material earMat = new Material(urpLit);
                earMat.color = new Color(1f, 0.8f, 0.85f);
                ear.GetComponent<Renderer>().material = earMat;

                // Ear inner (darker pink strip)
                GameObject earInner = GameObject.CreatePrimitive(PrimitiveType.Cube);
                earInner.name = "EarInner";
                earInner.transform.parent = ear.transform;
                earInner.transform.localPosition = new Vector3(0, 0, -0.05f);
                earInner.transform.localScale = new Vector3(0.6f, 0.8f, 0.5f);
                Object.DestroyImmediate(earInner.GetComponent<Collider>());

                Material innerMat = new Material(urpLit);
                innerMat.color = new Color(1f, 0.6f, 0.7f);
                earInner.GetComponent<Renderer>().material = innerMat;

            }

            // SIDE MIRRORS — mounted on thin arms extending outward from each side
            // Like car side mirrors, visible from the main camera behind the rabbit
            for (int mirrorSide = -1; mirrorSide <= 1; mirrorSide += 2)
            {
                // Mirror support arm (thin cylinder from body to mirror)
                GameObject mirrorArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mirrorArm.name = mirrorSide < 0 ? "LeftMirrorArm" : "RightMirrorArm";
                mirrorArm.transform.parent = playerParent.transform;
                mirrorArm.transform.localPosition = new Vector3(mirrorSide * 0.5f, 0.7f, 0.15f);
                mirrorArm.transform.localScale = new Vector3(0.04f, 0.45f, 0.04f);
                mirrorArm.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                Object.DestroyImmediate(mirrorArm.GetComponent<Collider>());

                Material armMat = new Material(urpLit);
                armMat.color = new Color(0.3f, 0.3f, 0.3f);
                mirrorArm.GetComponent<Renderer>().material = armMat;

                // Mirror mount (positioned at the end of the arm)
                GameObject mirrorMount = new GameObject(mirrorSide < 0 ? "LeftMirrorMount" : "RightMirrorMount");
                mirrorMount.transform.parent = playerParent.transform;
                mirrorMount.transform.localPosition = new Vector3(mirrorSide * 1.0f, 0.7f, 0.15f);
                mirrorMount.transform.localRotation = Quaternion.Euler(10f, mirrorSide * -35f, 0);

                // Mirror frame (dark backing) — 3x larger
                GameObject mirrorFrame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mirrorFrame.name = "MirrorFrame";
                mirrorFrame.transform.parent = mirrorMount.transform;
                mirrorFrame.transform.localPosition = Vector3.zero;
                mirrorFrame.transform.localScale = new Vector3(1.05f, 0.75f, 0.04f);
                Object.DestroyImmediate(mirrorFrame.GetComponent<Collider>());

                Material frameMat = new Material(urpLit);
                frameMat.color = new Color(0.2f, 0.2f, 0.2f);
                mirrorFrame.GetComponent<Renderer>().material = frameMat;

                // Mirror surface (Quad with RenderTexture) — 3x larger
                // Quad default normal faces local +Z; we rotate 180° on Y so it faces -Z (toward the camera behind the rabbit)
                GameObject mirrorSurface = GameObject.CreatePrimitive(PrimitiveType.Quad);
                mirrorSurface.name = mirrorSide < 0 ? "LeftMirror" : "RightMirror";
                mirrorSurface.transform.parent = mirrorMount.transform;
                mirrorSurface.transform.localPosition = new Vector3(0, 0, -0.025f);
                mirrorSurface.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                mirrorSurface.transform.localScale = new Vector3(0.9f, 0.6f, 1f);
                Object.DestroyImmediate(mirrorSurface.GetComponent<Collider>());

                // RenderTexture for mirror camera feed
                RenderTexture mirrorRT = new RenderTexture(512, 384, 16);
                mirrorRT.name = mirrorSide < 0 ? "LeftMirror_RT" : "RightMirror_RT";

                // Use UNLIT shader so the RenderTexture displays at full brightness
                Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
                Material mirrorMat = new Material(urpUnlit != null ? urpUnlit : urpLit);
                mirrorMat.SetTexture("_BaseMap", mirrorRT);
                mirrorMat.name = $"{mirrorSurface.name}_Mat";
                mirrorSurface.GetComponent<Renderer>().material = mirrorMat;

                // Mirror camera — looks in the running direction (world -Z)
                // Parent is rotated 180° on Y, so local +Z forward becomes world -Z
                GameObject mirrorCamObj = new GameObject(mirrorSide < 0 ? "LeftMirrorCamera" : "RightMirrorCamera");
                mirrorCamObj.transform.parent = playerParent.transform;
                mirrorCamObj.transform.localPosition = new Vector3(mirrorSide * 0.5f, 1.0f, 1.5f);
                mirrorCamObj.transform.localRotation = Quaternion.Euler(5f, mirrorSide * -10f, 0f);

                Camera mirrorCam = mirrorCamObj.AddComponent<Camera>();
                mirrorCam.targetTexture = mirrorRT;
                mirrorCam.fieldOfView = 60f;
                mirrorCam.nearClipPlane = 0.3f;
                mirrorCam.farClipPlane = 150f;
                mirrorCam.depth = -1f;
                mirrorCamObj.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            }

            // CharacterController — skinWidth keeps rabbit above ground
            CharacterController cc = playerParent.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 0f, 0);
            cc.radius = 0.3f;
            cc.height = 1f;
            cc.skinWidth = 0.08f;

            // RabbitController
            playerParent.AddComponent<Player.RabbitController>();

            // Face backwards (rabbit runs in -Z but faces +Z)
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

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");

            // Farmer body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "FarmerBody";
            body.transform.parent = farmerParent.transform;
            body.transform.localPosition = new Vector3(0, 1f, 0);
            body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);

            var bodyRenderer = body.GetComponent<Renderer>();
            Material farmerMat = new Material(urpLit);
            farmerMat.color = new Color(0.3f, 0.15f, 0.05f); // dark brown overalls
            farmerMat.name = "Farmer_Body_Mat";
            bodyRenderer.material = farmerMat;

            // Head
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "FarmerHead";
            head.transform.parent = farmerParent.transform;
            head.transform.localPosition = new Vector3(0, 2.1f, 0);
            head.transform.localScale = new Vector3(0.5f, 0.55f, 0.5f);
            Object.DestroyImmediate(head.GetComponent<Collider>());

            var headRenderer = head.GetComponent<Renderer>();
            Material skinMat = new Material(urpLit);
            skinMat.color = new Color(0.9f, 0.72f, 0.55f); // skin tone
            skinMat.name = "Farmer_Skin_Mat";
            headRenderer.material = skinMat;

            // Angry eyes (two small dark spheres)
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = side < 0 ? "LeftEye" : "RightEye";
                eye.transform.parent = head.transform;
                eye.transform.localPosition = new Vector3(side * 0.25f, 0.15f, 0.85f);
                eye.transform.localScale = new Vector3(0.25f, 0.15f, 0.15f);
                Object.DestroyImmediate(eye.GetComponent<Collider>());

                var eyeRenderer = eye.GetComponent<Renderer>();
                Material eyeMat = new Material(urpLit);
                eyeMat.color = Color.white;
                eyeRenderer.material = eyeMat;

                // Pupils
                GameObject pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pupil.name = "Pupil";
                pupil.transform.parent = eye.transform;
                pupil.transform.localPosition = new Vector3(0, 0, 0.4f);
                pupil.transform.localScale = new Vector3(0.5f, 0.7f, 0.5f);
                Object.DestroyImmediate(pupil.GetComponent<Collider>());

                var pupilRenderer = pupil.GetComponent<Renderer>();
                Material pupilMat = new Material(urpLit);
                pupilMat.color = new Color(0.15f, 0.05f, 0.0f); // dark angry eyes
                pupilRenderer.material = pupilMat;

                // Angry eyebrows (angled cubes above eyes)
                GameObject brow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                brow.name = side < 0 ? "LeftBrow" : "RightBrow";
                brow.transform.parent = head.transform;
                brow.transform.localPosition = new Vector3(side * 0.25f, 0.35f, 0.85f);
                brow.transform.localScale = new Vector3(0.3f, 0.06f, 0.1f);
                brow.transform.localRotation = Quaternion.Euler(0, 0, side * 25f); // angry angle
                Object.DestroyImmediate(brow.GetComponent<Collider>());

                var browRenderer = brow.GetComponent<Renderer>();
                Material browMat = new Material(urpLit);
                browMat.color = new Color(0.25f, 0.15f, 0.05f);
                browRenderer.material = browMat;
            }

            // Angry mouth (flat red cube)
            GameObject mouth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mouth.name = "Mouth";
            mouth.transform.parent = head.transform;
            mouth.transform.localPosition = new Vector3(0, -0.2f, 0.9f);
            mouth.transform.localScale = new Vector3(0.35f, 0.08f, 0.05f);
            mouth.transform.localRotation = Quaternion.Euler(0, 0, 5f);
            Object.DestroyImmediate(mouth.GetComponent<Collider>());

            var mouthRenderer = mouth.GetComponent<Renderer>();
            Material mouthMat = new Material(urpLit);
            mouthMat.color = new Color(0.6f, 0.15f, 0.1f);
            mouthRenderer.material = mouthMat;

            // Straw hat
            GameObject hat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hat.name = "FarmerHat";
            hat.transform.parent = farmerParent.transform;
            hat.transform.localPosition = new Vector3(0, 2.5f, 0);
            hat.transform.localScale = new Vector3(0.9f, 0.1f, 0.9f);
            Object.DestroyImmediate(hat.GetComponent<Collider>());

            var hatRenderer = hat.GetComponent<Renderer>();
            Material hatMat = new Material(urpLit);
            hatMat.color = new Color(0.85f, 0.78f, 0.45f); // straw color
            hatMat.name = "Farmer_Hat_Mat";
            hatRenderer.material = hatMat;

            // Hat top
            GameObject hatTop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hatTop.name = "HatTop";
            hatTop.transform.parent = farmerParent.transform;
            hatTop.transform.localPosition = new Vector3(0, 2.65f, 0);
            hatTop.transform.localScale = new Vector3(0.5f, 0.15f, 0.5f);
            Object.DestroyImmediate(hatTop.GetComponent<Collider>());

            var hatTopRenderer = hatTop.GetComponent<Renderer>();
            hatTopRenderer.material = hatMat;

            // Pitchfork handle
            GameObject fork = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fork.name = "PitchforkHandle";
            fork.transform.parent = farmerParent.transform;
            fork.transform.localPosition = new Vector3(0.5f, 1.3f, 0.2f);
            fork.transform.localScale = new Vector3(0.04f, 0.9f, 0.04f);
            fork.transform.localRotation = Quaternion.Euler(0, 0, -15f);
            Object.DestroyImmediate(fork.GetComponent<Collider>());

            var forkRenderer = fork.GetComponent<Renderer>();
            Material forkMat = new Material(urpLit);
            forkMat.color = new Color(0.5f, 0.35f, 0.15f); // wood
            forkRenderer.material = forkMat;

            // Pitchfork prongs
            for (int i = -1; i <= 1; i++)
            {
                GameObject prong = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                prong.name = $"Prong_{i+1}";
                prong.transform.parent = fork.transform;
                prong.transform.localPosition = new Vector3(i * 1.5f, -1.1f, 0);
                prong.transform.localScale = new Vector3(0.4f, 0.25f, 0.4f);
                Object.DestroyImmediate(prong.GetComponent<Collider>());

                var prongRenderer = prong.GetComponent<Renderer>();
                Material prongMat = new Material(urpLit);
                prongMat.color = new Color(0.6f, 0.6f, 0.6f); // metal
                prongRenderer.material = prongMat;
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

    }
}
