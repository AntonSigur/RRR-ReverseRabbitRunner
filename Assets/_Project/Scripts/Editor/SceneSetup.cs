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

        [MenuItem("ReverseRabbitRunner/Setup Game Scene")]
        public static void SetupGameScene()
        {
            // Safety: ensure we're editing SampleScene, not MainMenu
            var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (activeScene.name == "MainMenu")
            {
                if (!EditorUtility.DisplayDialog("Wrong Scene",
                    "You are in the MainMenu scene. Setup Game Scene should run on SampleScene.\n\nSwitch to SampleScene now?",
                    "Switch & Setup", "Cancel"))
                    return;
                UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            }

            CleanupScene();
            SetupLighting();
            CreateEditorPreviewGround();
            GameObject player = CreatePlayerPlaceholder();
            CreateFarmerPlaceholder(player.transform);
            CreateManagers();
            SetupEnvironmentSettings();

            Debug.Log("<b><color=#4CAF50>ReverseRabbitRunner</color></b>: Game scene setup complete! Chunks spawn at runtime.");

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
                                   "[Environment]", "[Cameras]", "[Sun]", "[Carrots]", "[Chunks]" };
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
            sun.shadowStrength = 0.85f;
            // Light comes from behind the camera / farmer side (+Z), low angle for long shadows.
            // Shadow projects in -Z (running direction) so it's visible in the mirrors.
            sunObj.transform.rotation = Quaternion.Euler(30f, 180f, 0f);
        }

        private static GameObject CreateGround()
        {
            // LEGACY — kept for reference, chunks now handle runtime ground
            return CreateEditorPreviewGround();
        }

        /// <summary>
        /// Small ground visible in the editor. ChunkManager generates the real ground at runtime.
        /// Tagged so ChunkManager can optionally destroy it on Start.
        /// </summary>
        private static GameObject CreateEditorPreviewGround()
        {
            GameObject groundParent = new GameObject("[Ground]");

            float previewLength = 100f;
            float groundWidth = MaxLanes * LaneWidth + 4f;

            // Farm ground preview
            GameObject farmGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
            farmGround.name = "PreviewGround";
            farmGround.transform.parent = groundParent.transform;
            farmGround.transform.localScale = new Vector3(groundWidth + 50f, 0.1f, previewLength);
            farmGround.transform.position = new Vector3(0, -0.05f, -previewLength / 2f + 25f);

            Material groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMat.color = new Color(0.45f, 0.30f, 0.15f);
            farmGround.GetComponent<Renderer>().material = groundMat;

            // Running path preview
            GameObject path = GameObject.CreatePrimitive(PrimitiveType.Cube);
            path.name = "PreviewPath";
            path.transform.parent = groundParent.transform;
            path.transform.localScale = new Vector3(MaxLanes * LaneWidth, 0.12f, previewLength);
            path.transform.position = new Vector3(0, -0.04f, -previewLength / 2f + 25f);

            Material pathMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            pathMat.color = new Color(0.55f, 0.40f, 0.25f);
            path.GetComponent<Renderer>().material = pathMat;

            return groundParent;
        }

        // CreateLaneMarkers — now handled by ChunkManager at runtime

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

            // SIDE MIRRORS — rabbit holds them out like hand mirrors
            // Camera is behind rabbit (at playerParent local -Z direction)
            // Mirror faces must face local -Z (toward camera). Quad faces local +Z by default.
            for (int mirrorSide = -1; mirrorSide <= 1; mirrorSide += 2)
            {
                string side = mirrorSide < 0 ? "Left" : "Right";

                // Rabbit arm (cylinder from body outward)
                GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                arm.name = $"{side}Arm";
                arm.transform.parent = playerParent.transform;
                arm.transform.localPosition = new Vector3(mirrorSide * 0.55f, 0.5f, 0f);
                arm.transform.localScale = new Vector3(0.08f, 1.0f, 0.08f);
                arm.transform.localRotation = Quaternion.Euler(0, 0, mirrorSide * -70f);
                Object.DestroyImmediate(arm.GetComponent<Collider>());
                Material armMat = new Material(urpLit);
                armMat.color = new Color(0.85f, 0.85f, 0.85f);
                arm.GetComponent<Renderer>().material = armMat;

                // Paw/hand (small sphere at end of arm)
                GameObject paw = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                paw.name = $"{side}Paw";
                paw.transform.parent = playerParent.transform;
                paw.transform.localPosition = new Vector3(mirrorSide * 2.0f, 0.8f, 0f);
                paw.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                Object.DestroyImmediate(paw.GetComponent<Collider>());
                Material pawMat = new Material(urpLit);
                pawMat.color = new Color(1f, 0.8f, 0.8f);
                paw.GetComponent<Renderer>().material = pawMat;

                // Mirror glass only (no frame box) — pushed further from rabbit
                // Assembly positioned beyond the paw so glass doesn't clip
                GameObject mirrorAssembly = new GameObject($"{side}MirrorAssembly");
                mirrorAssembly.transform.parent = playerParent.transform;
                mirrorAssembly.transform.localPosition = new Vector3(mirrorSide * 2.5f, 0.9f, 0f);
                mirrorAssembly.transform.localRotation = Quaternion.Euler(5f, mirrorSide * 15f, 0);

                // Mirror glass (Quad) — rotated 180° Y so visible face points local -Z (toward camera)
                GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Quad);
                glass.name = "Glass";
                glass.transform.parent = mirrorAssembly.transform;
                glass.transform.localPosition = Vector3.zero;
                glass.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                glass.transform.localScale = new Vector3(2.7f, 1.8f, 1f);
                Object.DestroyImmediate(glass.GetComponent<Collider>());

                // RenderTexture for mirror camera feed
                RenderTexture mirrorRT = new RenderTexture(512, 384, 16);
                mirrorRT.name = $"{side}Mirror_RT";

                Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
                Material glassMat = new Material(urpUnlit != null ? urpUnlit : urpLit);
                glassMat.SetTexture("_BaseMap", mirrorRT);
                glassMat.name = $"{side}Glass_Mat";
                glass.GetComponent<Renderer>().material = glassMat;

                // Mirror camera — at rabbit's head, looking forward (running direction)
                // playerParent local +Z = world -Z (running direction)
                GameObject camObj = new GameObject($"{side}MirrorCamera");
                camObj.transform.parent = playerParent.transform;
                camObj.transform.localPosition = new Vector3(mirrorSide * 0.3f, 1.0f, 0.8f);
                camObj.transform.localRotation = Quaternion.Euler(8f, mirrorSide * 5f, 0f);

                Camera mirrorCam = camObj.AddComponent<Camera>();
                mirrorCam.targetTexture = mirrorRT;
                mirrorCam.fieldOfView = 80f;
                mirrorCam.nearClipPlane = 0.5f;
                mirrorCam.farClipPlane = 200f;
                mirrorCam.depth = -1f;
                camObj.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            }

            // CharacterController — skinWidth keeps rabbit above ground
            CharacterController cc = playerParent.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 0f, 0);
            cc.radius = 0.3f;
            cc.height = 1f;
            cc.skinWidth = 0.08f;

            // RabbitController
            playerParent.AddComponent<Player.RabbitController>();
            playerParent.AddComponent<Player.MirrorCamera>();

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
            hatMat.color = new Color(0.85f, 0.78f, 0.45f);
            hatMat.name = "Farmer_Hat_Mat";
            hatRenderer.material = hatMat;

            // Hat top
            GameObject hatTop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hatTop.name = "HatTop";
            hatTop.transform.parent = farmerParent.transform;
            hatTop.transform.localPosition = new Vector3(0, 2.65f, 0);
            hatTop.transform.localScale = new Vector3(0.5f, 0.15f, 0.5f);
            Object.DestroyImmediate(hatTop.GetComponent<Collider>());
            hatTop.GetComponent<Renderer>().material = hatMat;

            // Farmer arms — properly rigged with pivot at shoulder end
            // Unity cylinders pivot at CENTER, so we offset by half-height to pivot from end
            Material sleevesMat = new Material(urpLit);
            sleevesMat.color = new Color(0.3f, 0.15f, 0.05f);
            Material skinHandMat = new Material(urpLit);
            skinHandMat.color = new Color(0.51f, 0.31f, 0.125f); // #825020 — user-chosen joint color

            // Helper: Unity cylinder is 2 units tall, scale.y = len/2
            // To pivot from top end: offset child by -len/2

            // --- LEFT ARM (user-adjusted independently — NOT mirrored) ---
            {
                GameObject shoulderPivot = new GameObject("LeftShoulderPivot");
                shoulderPivot.transform.parent = farmerParent.transform;
                shoulderPivot.transform.localPosition = new Vector3(-0.42f, 1.7f, 0f);
                shoulderPivot.transform.localRotation = new Quaternion(0, 0, 0.21643962f, 0.976296f);

                GameObject shoulder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                shoulder.name = "LeftShoulder";
                shoulder.transform.parent = shoulderPivot.transform;
                shoulder.transform.localPosition = new Vector3(0.013f, -0.006f, -0.042f);
                shoulder.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
                Object.DestroyImmediate(shoulder.GetComponent<Collider>());
                shoulder.GetComponent<Renderer>().material = skinHandMat;

                GameObject upperArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                upperArm.name = "LeftUpperArm";
                upperArm.transform.parent = shoulderPivot.transform;
                upperArm.transform.localPosition = new Vector3(-0.203f, -0.088f, -0.043f);
                upperArm.transform.localRotation = new Quaternion(0.0014534551f, -0.004539563f, 0.7904421f, 0.61251825f);
                upperArm.transform.localScale = new Vector3(0.1f, 0.2f, 0.1f);
                Object.DestroyImmediate(upperArm.GetComponent<Collider>());
                upperArm.GetComponent<Renderer>().material = sleevesMat;

                GameObject forearm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                forearm.name = "LeftForearm";
                forearm.transform.parent = shoulderPivot.transform;
                forearm.transform.localPosition = new Vector3(-0.488f, -0.187f, 0.101f);
                forearm.transform.localRotation = new Quaternion(-0.24558945f, 0.37155488f, -0.4684126f, 0.76303506f);
                forearm.transform.localScale = new Vector3(0.09f, 0.175f, 0.09f);
                Object.DestroyImmediate(forearm.GetComponent<Collider>());
                forearm.GetComponent<Renderer>().material = sleevesMat;

                GameObject handElbow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                handElbow.name = "LeftHandElbow";
                handElbow.transform.parent = shoulderPivot.transform;
                handElbow.transform.localPosition = new Vector3(-0.39f, -0.126f, -0.019f);
                handElbow.transform.localScale = new Vector3(0.13f, 0.13f, 0.13f);
                Object.DestroyImmediate(handElbow.GetComponent<Collider>());
                handElbow.GetComponent<Renderer>().material = skinHandMat;

                GameObject fingers = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fingers.name = "LeftHandFingers";
                fingers.transform.parent = shoulderPivot.transform;
                fingers.transform.localPosition = new Vector3(-0.591f, -0.267f, 0.251f);
                fingers.transform.localScale = new Vector3(0.13f, 0.13f, 0.13f);
                Object.DestroyImmediate(fingers.GetComponent<Collider>());
                fingers.GetComponent<Renderer>().material = skinHandMat;
            }

            // --- RIGHT ARM + FORK (under "ForkPivot" — rotates from shoulder for wave) ---
            // All positions hand-tuned by user in Unity editor
            {
                GameObject forkPivot = new GameObject("ForkPivot");
                forkPivot.transform.parent = farmerParent.transform;
                forkPivot.transform.localPosition = new Vector3(0.42f, 1.7f, 0f);
                forkPivot.transform.localRotation = new Quaternion(0, 0, -0.21643962f, 0.976296f);

                GameObject shoulder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                shoulder.name = "RightShoulder";
                shoulder.transform.parent = forkPivot.transform;
                shoulder.transform.localPosition = new Vector3(-0.013f, -0.006f, -0.042f);
                shoulder.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
                Object.DestroyImmediate(shoulder.GetComponent<Collider>());
                shoulder.GetComponent<Renderer>().material = skinHandMat;

                GameObject upperArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                upperArm.name = "RightUpperArm";
                upperArm.transform.parent = forkPivot.transform;
                upperArm.transform.localPosition = new Vector3(0.2f, -0.087f, -0.05f);
                upperArm.transform.localRotation = new Quaternion(0.024632243f, -0.038542166f, -0.7766107f, 0.62831813f);
                upperArm.transform.localScale = new Vector3(0.1f, 0.2f, 0.1f);
                Object.DestroyImmediate(upperArm.GetComponent<Collider>());
                upperArm.GetComponent<Renderer>().material = sleevesMat;

                GameObject forearm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                forearm.name = "RightForearm";
                forearm.transform.parent = forkPivot.transform;
                forearm.transform.localPosition = new Vector3(0.549f, -0.165f, 0.005f);
                forearm.transform.localRotation = new Quaternion(-0.093125224f, -0.14372422f, 0.5803505f, 0.796156f);
                forearm.transform.localScale = new Vector3(0.09f, 0.175f, 0.09f);
                Object.DestroyImmediate(forearm.GetComponent<Collider>());
                forearm.GetComponent<Renderer>().material = sleevesMat;

                // Elbow joint
                GameObject handElbow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                handElbow.name = "RightHandElbow";
                handElbow.transform.parent = forkPivot.transform;
                handElbow.transform.localPosition = new Vector3(0.409f, -0.12f, -0.036f);
                handElbow.transform.localScale = new Vector3(0.13f, 0.13f, 0.13f);
                Object.DestroyImmediate(handElbow.GetComponent<Collider>());
                handElbow.GetComponent<Renderer>().material = skinHandMat;

                // Fingers gripping fork shaft
                GameObject fingers = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fingers.name = "RightHandFingers";
                fingers.transform.parent = forkPivot.transform;
                fingers.transform.localPosition = new Vector3(0.713f, -0.211f, 0.068f);
                fingers.transform.localScale = new Vector3(0.13f, 0.13f, 0.13f);
                Object.DestroyImmediate(fingers.GetComponent<Collider>());
                fingers.GetComponent<Renderer>().material = skinHandMat;

                // Pitchfork handle (tilted forward)
                GameObject fork = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fork.name = "PitchforkHandle";
                fork.transform.parent = forkPivot.transform;
                fork.transform.localPosition = new Vector3(0.615f, -0.008f, 0.145f);
                fork.transform.localRotation = new Quaternion(0.17398773f, 0.03857215f, 0.21297489f, 0.9606676f);
                fork.transform.localScale = new Vector3(0.04f, 0.6f, 0.04f);
                Object.DestroyImmediate(fork.GetComponent<Collider>());

                Material forkMat = new Material(urpLit);
                forkMat.color = new Color(0.5f, 0.35f, 0.15f);
                fork.GetComponent<Renderer>().material = forkMat;

                // Fork head — crossbar + 3 prongs (user-positioned, tilted to match handle)
                Material prongMat = new Material(urpLit);
                prongMat.color = new Color(0.6f, 0.6f, 0.6f);

                GameObject crossbar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                crossbar.name = "ForkCrossbar";
                crossbar.transform.parent = forkPivot.transform;
                crossbar.transform.localPosition = new Vector3(0.372f, 0.505f, 0.358f);
                crossbar.transform.localRotation = new Quaternion(0.16746448f, -0.060952112f, 0.695788f, 0.695787f);
                crossbar.transform.localScale = new Vector3(0.03f, 0.06f, 0.03f);
                Object.DestroyImmediate(crossbar.GetComponent<Collider>());
                crossbar.GetComponent<Renderer>().material = prongMat;

                Quaternion prongRot = new Quaternion(0.17398773f, 0.03857215f, 0.21297489f, 0.9606676f);

                GameObject prong0 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                prong0.name = "Prong_0";
                prong0.transform.parent = forkPivot.transform;
                prong0.transform.localPosition = new Vector3(0.275f, 0.61f, 0.408f);
                prong0.transform.localRotation = prongRot;
                prong0.transform.localScale = new Vector3(0.02f, 0.12f, 0.02f);
                Object.DestroyImmediate(prong0.GetComponent<Collider>());
                prong0.GetComponent<Renderer>().material = prongMat;

                GameObject prong1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                prong1.name = "Prong_1";
                prong1.transform.parent = forkPivot.transform;
                prong1.transform.localPosition = new Vector3(0.327f, 0.608f, 0.4f);
                prong1.transform.localRotation = prongRot;
                prong1.transform.localScale = new Vector3(0.02f, 0.12f, 0.02f);
                Object.DestroyImmediate(prong1.GetComponent<Collider>());
                prong1.GetComponent<Renderer>().material = prongMat;

                GameObject prong2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                prong2.name = "Prong_2";
                prong2.transform.parent = forkPivot.transform;
                prong2.transform.localPosition = new Vector3(0.388f, 0.605f, 0.389f);
                prong2.transform.localRotation = prongRot;
                prong2.transform.localScale = new Vector3(0.02f, 0.12f, 0.02f);
                Object.DestroyImmediate(prong2.GetComponent<Collider>());
                prong2.GetComponent<Renderer>().material = prongMat;
            }

            farmerParent.AddComponent<Enemies.FarmerController>();
            farmerParent.AddComponent<Enemies.FarmerForkWave>();

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

            // Chunk-based infinite world (replaces static ground/carrots)
            GameObject chunkObj = new GameObject("[Chunks]");
            chunkObj.AddComponent<World.ChunkManager>();

            GameObject osObj = new GameObject("ObstacleSpawner");
            osObj.transform.parent = managers.transform;
            osObj.AddComponent<World.ObstacleSpawner>();

            // HUD (also handles pause menu via OnGUI)
            GameObject hudObj = new GameObject("GameHUD");
            hudObj.transform.parent = managers.transform;
            hudObj.AddComponent<UI.GameHUD>();

            // Event System for UI (check if one exists first)
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSys = new GameObject("EventSystem");
                eventSys.transform.parent = managers.transform;
                eventSys.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSys.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // Auto-start helper
            GameObject autoStart = new GameObject("AutoStart");
            autoStart.transform.parent = managers.transform;
            autoStart.AddComponent<Core.AutoStartGame>();

            return managers;
        }

        // CreateSampleCarrots — now handled by ChunkManager at runtime

        private static void SetupEnvironmentSettings()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.75f, 0.9f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.75f, 0.85f, 0.95f);
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 300f;
        }

    }
}
