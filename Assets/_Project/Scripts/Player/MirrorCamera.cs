using UnityEngine;
using UnityEngine.InputSystem;

namespace ReverseRabbitRunner.Player
{
    /// <summary>
    /// Manages the mirror-view cameras on the rabbit's ears.
    /// Renders to RenderTextures displayed as UI elements (side mirrors).
    /// Runtime keyboard controls for adjusting mirrors like car mirrors:
    ///   Numpad 4/6: rotate left mirror left/right  |  Numpad 7/9: rotate right mirror left/right
    ///   Numpad 2/8: tilt both mirrors down/up
    ///   Numpad +/-: zoom in/out (FOV)
    ///   Numpad 5: reset mirrors to default
    /// </summary>
    public class MirrorCamera : MonoBehaviour
    {
        [Header("Mirror Settings")]
        [SerializeField] private float mirrorFOV = 80f;
        [SerializeField] private float mirrorRange = 200f;

        [Header("Adjustment Limits")]
        [SerializeField] private float maxYawOffset = 40f;
        [SerializeField] private float maxPitchOffset = 30f;
        [SerializeField] private float minFOV = 40f;
        [SerializeField] private float maxFOV = 120f;
        [SerializeField] private float adjustSpeed = 40f;
        [SerializeField] private float zoomSpeed = 30f;

        [Header("Dirty Carrot Effect")]
        [SerializeField] private float dirtyObscureAmount = 0f;

        private Camera leftCam;
        private Camera rightCam;
        private Transform leftAssembly;
        private Transform rightAssembly;

        private float leftYawOffset = 0f;
        private float rightYawOffset = 0f;
        private float pitchOffset = 0f;
        private float currentFOV;

        private Quaternion leftBaseRotation;
        private Quaternion rightBaseRotation;

        private bool isDirty = false;
        private float dirtyTimer = 0f;

        public float DirtyObscureAmount => dirtyObscureAmount;

        private void Start()
        {
            currentFOV = mirrorFOV;
            FindMirrorComponents();
        }

        private void FindMirrorComponents()
        {
            // Auto-find mirror cameras and assemblies in children of our parent (the player)
            Transform player = transform;
            foreach (Transform child in player)
            {
                if (child.name == "LeftMirrorCamera")
                    leftCam = child.GetComponent<Camera>();
                else if (child.name == "RightMirrorCamera")
                    rightCam = child.GetComponent<Camera>();
                else if (child.name == "LeftMirrorAssembly")
                    leftAssembly = child;
                else if (child.name == "RightMirrorAssembly")
                    rightAssembly = child;
            }

            if (leftAssembly != null) leftBaseRotation = leftAssembly.localRotation;
            if (rightAssembly != null) rightBaseRotation = rightAssembly.localRotation;

            ApplyFOV();
        }

        private void Update()
        {
            if (isDirty)
            {
                dirtyTimer -= Time.deltaTime;
                if (dirtyTimer <= 0f)
                    ClearDirtyEffect();
            }

            HandleMirrorAdjustment();
        }

        private void HandleMirrorAdjustment()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            float dt = Time.deltaTime;

            // Left mirror yaw: Numpad 4/6
            if (kb.numpad4Key.isPressed)
                leftYawOffset = Mathf.Clamp(leftYawOffset - adjustSpeed * dt, -maxYawOffset, maxYawOffset);
            if (kb.numpad6Key.isPressed)
                leftYawOffset = Mathf.Clamp(leftYawOffset + adjustSpeed * dt, -maxYawOffset, maxYawOffset);

            // Right mirror yaw: Numpad 7/9
            if (kb.numpad7Key.isPressed)
                rightYawOffset = Mathf.Clamp(rightYawOffset - adjustSpeed * dt, -maxYawOffset, maxYawOffset);
            if (kb.numpad9Key.isPressed)
                rightYawOffset = Mathf.Clamp(rightYawOffset + adjustSpeed * dt, -maxYawOffset, maxYawOffset);

            // Both mirrors pitch: Numpad 8/2
            if (kb.numpad8Key.isPressed)
                pitchOffset = Mathf.Clamp(pitchOffset - adjustSpeed * dt, -maxPitchOffset, maxPitchOffset);
            if (kb.numpad2Key.isPressed)
                pitchOffset = Mathf.Clamp(pitchOffset + adjustSpeed * dt, -maxPitchOffset, maxPitchOffset);

            // Zoom: Numpad +/-
            if (kb.numpadPlusKey.isPressed)
                currentFOV = Mathf.Clamp(currentFOV - zoomSpeed * dt, minFOV, maxFOV);
            if (kb.numpadMinusKey.isPressed)
                currentFOV = Mathf.Clamp(currentFOV + zoomSpeed * dt, minFOV, maxFOV);

            // Reset: Numpad 5
            if (kb.numpad5Key.wasPressedThisFrame)
            {
                leftYawOffset = 0f;
                rightYawOffset = 0f;
                pitchOffset = 0f;
                currentFOV = mirrorFOV;
            }

            // Apply rotations to mirror assemblies (affects Quad orientation = what player sees)
            if (leftAssembly != null)
                leftAssembly.localRotation = leftBaseRotation * Quaternion.Euler(pitchOffset, leftYawOffset, 0);
            if (rightAssembly != null)
                rightAssembly.localRotation = rightBaseRotation * Quaternion.Euler(pitchOffset, rightYawOffset, 0);

            ApplyFOV();
        }

        private void ApplyFOV()
        {
            if (leftCam != null)
            {
                leftCam.fieldOfView = currentFOV;
                leftCam.farClipPlane = mirrorRange;
            }
            if (rightCam != null)
            {
                rightCam.fieldOfView = currentFOV;
                rightCam.farClipPlane = mirrorRange;
            }
        }

        public void ApplyDirtyEffect(float duration, float obscureAmount = 0.7f)
        {
            isDirty = true;
            dirtyTimer = duration;
            dirtyObscureAmount = obscureAmount;
        }

        public void ClearDirtyEffect()
        {
            isDirty = false;
            dirtyTimer = 0f;
            dirtyObscureAmount = 0f;
        }
    }
}
