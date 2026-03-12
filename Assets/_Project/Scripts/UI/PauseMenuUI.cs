using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReverseRabbitRunner.UI
{
    /// <summary>
    /// In-game pause menu with Resume, Settings, Quit to Menu.
    /// Toggled with Escape key. Pauses game via Time.timeScale.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        private Canvas canvas;
        private GameObject pausePanel;
        private GameObject settingsPanel;
        private Slider volumeSlider;
        private bool isPaused;

        public bool IsPaused => isPaused;

        private void Start()
        {
            Debug.Log("[PauseMenuUI] Initializing...");
            try
            {
                BuildUI();
                Debug.Log("[PauseMenuUI] Ready — press Esc or Q to pause");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PauseMenuUI] BuildUI failed: {e.Message}\n{e.StackTrace}");
            }
        }

        private void Update()
        {
            // Check for pause key — support both new and old Input System
            bool pausePressed = false;

            // New Input System
            if (Keyboard.current != null)
            {
                pausePressed = Keyboard.current.escapeKey.wasPressedThisFrame
                            || Keyboard.current.qKey.wasPressedThisFrame;
            }

            // Old Input System fallback
            if (!pausePressed)
            {
                pausePressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Q);
            }

            if (pausePressed)
            {
                if (settingsPanel != null && settingsPanel.activeSelf)
                    OnSettingsBack();
                else
                    TogglePause();
            }
        }

        public void TogglePause()
        {
            // Don't pause if game is over
            var gm = Core.GameManager.Instance;
            if (gm != null && gm.CurrentState == Core.GameManager.GameState.GameOver)
                return;

            isPaused = !isPaused;
            pausePanel.SetActive(isPaused);
            Time.timeScale = isPaused ? 0f : 1f;

            Cursor.visible = isPaused;
            Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;
        }

        private void BuildUI()
        {
            // Canvas
            GameObject canvasObj = new GameObject("PauseMenuCanvas");
            canvasObj.transform.SetParent(transform, false);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // Pause panel (hidden by default)
            pausePanel = new GameObject("PausePanel");
            pausePanel.transform.SetParent(canvasObj.transform, false);
            RectTransform panelRect = pausePanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;

            // Dark overlay
            Image overlay = pausePanel.AddComponent<Image>();
            overlay.color = new Color(0, 0, 0, 0.7f);

            // Title
            CreateText(pausePanel.transform, "PauseTitle", "PAUSED",
                new Vector2(0, 150), 48, Color.white, FontStyle.Bold);

            // Buttons
            CreateButton(pausePanel.transform, "ResumeBtn", "▶  RESUME", new Vector2(0, 40), OnResume,
                new Color(0.2f, 0.7f, 0.2f), Color.white, 32);

            CreateButton(pausePanel.transform, "PauseSettingsBtn", "⚙  SETTINGS", new Vector2(0, -40), OnSettings,
                new Color(0.3f, 0.3f, 0.4f), Color.white, 26);

            CreateButton(pausePanel.transform, "QuitToMenuBtn", "✕  QUIT TO MENU", new Vector2(0, -115), OnQuitToMenu,
                new Color(0.5f, 0.15f, 0.15f), Color.white, 26);

            // Settings sub-panel
            BuildSettingsPanel(pausePanel.transform);

            pausePanel.SetActive(false);
        }

        private void BuildSettingsPanel(Transform parent)
        {
            settingsPanel = new GameObject("SettingsPanel");
            settingsPanel.transform.SetParent(parent, false);
            RectTransform panelRect = settingsPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(500, 380);
            panelRect.anchoredPosition = Vector2.zero;

            Image bg = settingsPanel.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.1f, 0.18f, 0.98f);

            CreateText(settingsPanel.transform, "SettingsTitle", "SETTINGS",
                new Vector2(0, 140), 32, Color.white, FontStyle.Bold);

            // Volume
            CreateText(settingsPanel.transform, "VolLabel", "Master Volume",
                new Vector2(0, 80), 20, new Color(0.8f, 0.8f, 0.8f));

            GameObject sliderObj = CreateSlider(settingsPanel.transform, new Vector2(0, 45));
            volumeSlider = sliderObj.GetComponent<Slider>();
            volumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

            // Controls
            CreateText(settingsPanel.transform, "CtrlTitle", "CONTROLS",
                new Vector2(0, -10), 20, new Color(0.8f, 0.8f, 0.8f));

            CreateText(settingsPanel.transform, "CtrlPC",
                "A/D or ←/→: Switch lanes\nNumpad: Adjust mirrors\nEsc: Pause/Resume",
                new Vector2(0, -65), 16, new Color(0.6f, 0.6f, 0.6f));

            CreateText(settingsPanel.transform, "CtrlMobile",
                "Mobile: Swipe left/right",
                new Vector2(0, -110), 16, new Color(0.6f, 0.6f, 0.6f));

            CreateButton(settingsPanel.transform, "BackBtn", "← BACK", new Vector2(0, -155), OnSettingsBack,
                new Color(0.3f, 0.3f, 0.4f), Color.white, 22);

            settingsPanel.SetActive(false);
        }

        private void OnResume()
        {
            TogglePause();
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

        private void OnQuitToMenu()
        {
            Time.timeScale = 1f;
            isPaused = false;
            SceneManager.LoadScene("MainMenu");
        }

        // --- UI Helpers (same pattern as MainMenuUI) ---

        private static Font GetFont()
        {
            // Unity 6 uses LegacyRuntime.ttf, older versions use Arial.ttf
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
            rect.sizeDelta = new Vector2(600, fontSize * 3 + 10);
            rect.anchoredPosition = pos;

            Text text = obj.AddComponent<Text>();
            text.text = content;
            text.font = GetFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;

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
            rect.sizeDelta = new Vector2(320, 50);
            rect.anchoredPosition = pos;

            Image img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = bgColor;
            colors.highlightedColor = bgColor * 1.2f;
            colors.pressedColor = bgColor * 0.8f;
            btn.colors = colors;

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

        private static GameObject CreateSlider(Transform parent, Vector2 pos)
        {
            GameObject sliderObj = new GameObject("VolumeSlider");
            sliderObj.transform.SetParent(parent, false);
            RectTransform rt = sliderObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(280, 25);
            rt.anchoredPosition = pos;

            // Background
            GameObject bg = new GameObject("Background");
            bg.transform.SetParent(sliderObj.transform, false);
            Image bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f);
            RectTransform bgR = bg.GetComponent<RectTransform>();
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
            RectTransform har = handleArea.AddComponent<RectTransform>();
            har.anchorMin = Vector2.zero;
            har.anchorMax = Vector2.one;
            har.sizeDelta = new Vector2(-20, 0);

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(18, 25);

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;
            slider.minValue = 0f;
            slider.maxValue = 1f;

            return sliderObj;
        }
    }
}
