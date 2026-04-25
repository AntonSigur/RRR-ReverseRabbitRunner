using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Cross-platform touch / swipe input. Emits high-level events
    /// (<see cref="OnSwipeLeft"/>, <see cref="OnSwipeRight"/>,
    /// <see cref="OnSwipeUp"/>, <see cref="OnTap"/>) so gameplay code
    /// doesn't need to know about the underlying input device.
    ///
    /// Implementation notes:
    /// - Uses Unity's new Input System exclusively (project is in Input
    ///   System-only mode). Reads <see cref="Touchscreen.current"/> primary
    ///   touch — sufficient for an endless runner; multi-touch isn't needed.
    /// - Horizontal swipes fire <em>live</em> the moment the threshold is
    ///   crossed while the finger is still down. This feels much snappier
    ///   than waiting for touch-up. A per-touch latch prevents repeats.
    /// - Vertical "swipe up" and tap are detected on touch release.
    /// - Thresholds scale with screen DPI when available so a "swipe" is
    ///   roughly the same physical distance on phones and tablets.
    /// - Auto-spawned via <see cref="RuntimeInitializeOnLoadMethodAttribute"/>
    ///   as a DontDestroyOnLoad singleton; works in every scene without
    ///   relying on SceneSetup wiring.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [Header("Swipe (in millimetres of travel)")]
        [SerializeField] private float swipeThresholdMm = 8f;
        [SerializeField] private float maxTapDurationSeconds = 0.25f;
        [SerializeField] private float maxTapTravelMm = 4f;

        // Fallback DPI when the device reports 0 (e.g. desktop).
        private const float FallbackDpi = 160f;

        public event Action OnSwipeLeft;
        public event Action OnSwipeRight;
        public event Action OnSwipeUp;
        public event Action OnTap;

        private bool touchActive;
        private Vector2 touchStartPos;
        private float touchStartTime;
        private bool horizontalLatched;
        private bool verticalLatched;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[InputManager]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<InputManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private float PixelsPerMm
        {
            get
            {
                float dpi = Screen.dpi > 0f ? Screen.dpi : FallbackDpi;
                return dpi / 25.4f;
            }
        }

        private void Update()
        {
            var ts = Touchscreen.current;
            if (ts == null) return;

            var primary = ts.primaryTouch;
            bool isPressed = primary.press.isPressed;

            if (isPressed && !touchActive)
            {
                // Touch begin
                touchActive = true;
                touchStartPos = primary.position.ReadValue();
                touchStartTime = Time.unscaledTime;
                horizontalLatched = false;
                verticalLatched = false;
                return;
            }

            if (!isPressed)
            {
                if (touchActive)
                {
                    // Touch end — possibly a tap or an upward swipe-on-release.
                    Vector2 endPos = primary.position.ReadValue();
                    Vector2 delta = endPos - touchStartPos;
                    float duration = Time.unscaledTime - touchStartTime;
                    float ppmm = PixelsPerMm;

                    if (!horizontalLatched && !verticalLatched)
                    {
                        bool isTap =
                            duration <= maxTapDurationSeconds &&
                            delta.magnitude <= maxTapTravelMm * ppmm;

                        if (isTap)
                        {
                            OnTap?.Invoke();
                        }
                        else if (delta.y >= swipeThresholdMm * ppmm &&
                                 delta.y >= Mathf.Abs(delta.x))
                        {
                            OnSwipeUp?.Invoke();
                        }
                    }
                    touchActive = false;
                }
                return;
            }

            // Live tracking — fire horizontal swipe as soon as threshold is crossed.
            if (touchActive && !horizontalLatched)
            {
                Vector2 delta = primary.position.ReadValue() - touchStartPos;
                float threshold = swipeThresholdMm * PixelsPerMm;

                if (Mathf.Abs(delta.x) >= threshold && Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                {
                    horizontalLatched = true;
                    if (delta.x < 0f) OnSwipeLeft?.Invoke();
                    else              OnSwipeRight?.Invoke();
                }
                else if (!verticalLatched && delta.y >= threshold && delta.y >= Mathf.Abs(delta.x))
                {
                    // Live up-swipe so jump triggers without waiting for finger lift.
                    verticalLatched = true;
                    OnSwipeUp?.Invoke();
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
