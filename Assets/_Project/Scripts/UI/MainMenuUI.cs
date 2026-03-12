using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReverseRabbitRunner.UI
{
    /// <summary>
    /// Main Menu — Canvas UI with Play, Settings, Quit.
    /// Lives in its own scene (MainMenu).
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        private Canvas canvas;
        private GameObject settingsPanel;
        private Slider volumeSlider;

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            BuildUI();
        }

        private void BuildUI()
        {
            // Canvas
            GameObject canvasObj = new GameObject("MainMenuCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // Dark background
            GameObject bg = CreatePanel(canvasObj.transform, "Background", new Color(0.08f, 0.06f, 0.12f, 1f));
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // Title
            CreateText(canvasObj.transform, "Title", "🥕 REVERSE RABBIT RUNNER",
                new Vector2(0, 200), 64, new Color(1f, 0.6f, 0.1f), FontStyle.Bold);

            // Subtitle
            CreateText(canvasObj.transform, "Subtitle", "Run backwards. Trust your mirrors.",
                new Vector2(0, 120), 24, new Color(0.7f, 0.7f, 0.7f));

            // Buttons
            CreateButton(canvasObj.transform, "PlayButton", "▶  PLAY", new Vector2(0, 0), OnPlay,
                new Color(0.2f, 0.7f, 0.2f), Color.white, 36);

            CreateButton(canvasObj.transform, "SettingsButton", "⚙  SETTINGS", new Vector2(0, -80), OnSettings,
                new Color(0.3f, 0.3f, 0.4f), Color.white, 28);

            CreateButton(canvasObj.transform, "QuitButton", "✕  QUIT", new Vector2(0, -155), OnQuit,
                new Color(0.5f, 0.15f, 0.15f), Color.white, 28);

            // Version text
            CreateText(canvasObj.transform, "Version", "v0.1 — Prototype",
                new Vector2(0, -280), 16, new Color(0.4f, 0.4f, 0.4f));

            // Settings panel (hidden by default)
            BuildSettingsPanel(canvasObj.transform);
        }

        private void BuildSettingsPanel(Transform parent)
        {
            settingsPanel = CreatePanel(parent, "SettingsPanel", new Color(0.12f, 0.1f, 0.18f, 0.95f));
            RectTransform panelRect = settingsPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(500, 400);
            panelRect.anchoredPosition = Vector2.zero;

            CreateText(settingsPanel.transform, "SettingsTitle", "SETTINGS",
                new Vector2(0, 150), 36, Color.white, FontStyle.Bold);

            // Volume
            CreateText(settingsPanel.transform, "VolumeLabel", "Master Volume",
                new Vector2(0, 80), 22, new Color(0.8f, 0.8f, 0.8f));

            GameObject sliderObj = new GameObject("VolumeSlider");
            sliderObj.transform.SetParent(settingsPanel.transform, false);
            RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
            sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.sizeDelta = new Vector2(300, 30);
            sliderRect.anchoredPosition = new Vector2(0, 40);

            // Slider background
            GameObject sliderBg = new GameObject("Background");
            sliderBg.transform.SetParent(sliderObj.transform, false);
            Image bgImg = sliderBg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f);
            RectTransform bgR = sliderBg.GetComponent<RectTransform>();
            bgR.anchorMin = Vector2.zero;
            bgR.anchorMax = Vector2.one;
            bgR.sizeDelta = Vector2.zero;

            // Fill area
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = new Vector2(-20, 0);

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(1f, 0.6f, 0.1f);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            // Handle
            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObj.transform, false);
            RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.sizeDelta = new Vector2(-20, 0);

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 30);

            volumeSlider = sliderObj.AddComponent<Slider>();
            volumeSlider.fillRect = fillRect;
            volumeSlider.handleRect = handleRect;
            volumeSlider.targetGraphic = handleImg;
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

            // Controls hint
            CreateText(settingsPanel.transform, "ControlsTitle", "CONTROLS",
                new Vector2(0, -20), 22, new Color(0.8f, 0.8f, 0.8f));

            CreateText(settingsPanel.transform, "ControlsPC", "PC: A/D or ←/→ to switch lanes\nNumpad: Adjust mirrors\nEsc: Pause",
                new Vector2(0, -70), 18, new Color(0.6f, 0.6f, 0.6f));

            CreateText(settingsPanel.transform, "ControlsMobile", "Mobile: Swipe left/right to switch lanes",
                new Vector2(0, -120), 18, new Color(0.6f, 0.6f, 0.6f));

            // Back button
            CreateButton(settingsPanel.transform, "BackButton", "← BACK", new Vector2(0, -170), OnSettingsBack,
                new Color(0.3f, 0.3f, 0.4f), Color.white, 24);

            settingsPanel.SetActive(false);
        }

        private void OnPlay()
        {
            SceneManager.LoadScene("SampleScene");
        }

        private void OnSettings()
        {
            settingsPanel.SetActive(true);
        }

        private void OnSettingsBack()
        {
            settingsPanel.SetActive(false);
        }

        private void OnVolumeChanged(float value)
        {
            AudioListener.volume = value;
            PlayerPrefs.SetFloat("MasterVolume", value);
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // --- UI Helpers ---

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            Image img = panel.AddComponent<Image>();
            img.color = color;
            return panel;
        }

        private static Font GetFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            return font;
        }

        private static Text CreateText(Transform parent, string name, string content,
            Vector2 pos, int fontSize, Color color, FontStyle style = FontStyle.Normal)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(800, fontSize + 20);
            rect.anchoredPosition = pos;

            Text text = obj.AddComponent<Text>();
            text.text = content;
            text.font = GetFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label,
            Vector2 pos, UnityEngine.Events.UnityAction onClick, Color bgColor, Color textColor, int fontSize)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320, 55);
            rect.anchoredPosition = pos;

            Image img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;

            // Button hover/press colors
            var colors = btn.colors;
            colors.normalColor = bgColor;
            colors.highlightedColor = bgColor * 1.2f;
            colors.pressedColor = bgColor * 0.8f;
            btn.colors = colors;

            // Label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;

            Text text = labelObj.AddComponent<Text>();
            text.text = label;
            text.font = GetFont();
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = textColor;
            text.alignment = TextAnchor.MiddleCenter;

            btn.onClick.AddListener(onClick);

            return btn;
        }
    }
}
