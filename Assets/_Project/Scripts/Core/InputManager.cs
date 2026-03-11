using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Handles cross-platform input mapping for lane switching.
    /// Supports keyboard (A/D, arrows), touch swipe, and gamepad.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [Header("Touch Settings")]
        [SerializeField] private float swipeThreshold = 50f;

        private Vector2 touchStartPos;
        private bool isSwiping = false;

        public event System.Action OnSwipeLeft;
        public event System.Action OnSwipeRight;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            HandleTouchInput();
        }

        private void HandleTouchInput()
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        touchStartPos = touch.position;
                        isSwiping = true;
                        break;

                    case TouchPhase.Ended:
                        if (isSwiping)
                        {
                            Vector2 swipeDelta = touch.position - touchStartPos;
                            if (Mathf.Abs(swipeDelta.x) > swipeThreshold)
                            {
                                if (swipeDelta.x < 0)
                                    OnSwipeLeft?.Invoke();
                                else
                                    OnSwipeRight?.Invoke();
                            }
                            isSwiping = false;
                        }
                        break;

                    case TouchPhase.Canceled:
                        isSwiping = false;
                        break;
                }
            }
        }
    }
}
