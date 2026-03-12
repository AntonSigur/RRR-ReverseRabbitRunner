#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReverseRabbitRunner.Editor
{
    /// <summary>
    /// Editor tool to create the MainMenu scene with Canvas UI.
    /// </summary>
    public static class MainMenuSceneSetup
    {
        [MenuItem("ReverseRabbitRunner/Create Main Menu Scene")]
        public static void CreateMainMenuScene()
        {
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MainMenu";

            // Camera
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.06f, 0.12f);
            camObj.AddComponent<AudioListener>();

            // Event System (required for UI interaction)
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            // MainMenuUI script
            GameObject menuManager = new GameObject("[MainMenu]");
            menuManager.AddComponent<UI.MainMenuUI>();

            // Save scene
            string scenePath = "Assets/Scenes/MainMenu.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            // Add to Build Settings if not already there
            AddSceneToBuildSettings(scenePath);
            AddSceneToBuildSettings("Assets/Scenes/SampleScene.unity");

            Debug.Log("<b><color=#4CAF50>ReverseRabbitRunner</color></b>: Main Menu scene created and saved!");
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool found = false;
            foreach (var s in scenes)
            {
                if (s.path == scenePath) { found = true; break; }
            }
            if (!found)
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log($"Added '{scenePath}' to Build Settings");
            }
        }
    }
}
#endif
