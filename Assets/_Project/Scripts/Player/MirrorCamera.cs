using UnityEngine;

namespace ReverseRabbitRunner.Player
{
    /// <summary>
    /// Manages the mirror-view cameras on the rabbit's ears.
    /// Renders to RenderTextures displayed as UI elements (side mirrors).
    /// </summary>
    public class MirrorCamera : MonoBehaviour
    {
        [Header("Mirror Settings")]
        [SerializeField] private Camera leftMirrorCamera;
        [SerializeField] private Camera rightMirrorCamera;

        [Header("Render Textures")]
        [SerializeField] private RenderTexture leftMirrorTexture;
        [SerializeField] private RenderTexture rightMirrorTexture;

        [Header("Mirror Properties")]
        [SerializeField] private float mirrorFOV = 60f;
        [SerializeField] private float mirrorRange = 50f;

        [Header("Dirty Carrot Effect")]
        [SerializeField] private float dirtyObscureAmount = 0f;

        private bool isDirty = false;
        private float dirtyTimer = 0f;

        public float DirtyObscureAmount => dirtyObscureAmount;

        private void Start()
        {
            SetupMirrorCameras();
        }

        private void Update()
        {
            if (isDirty)
            {
                dirtyTimer -= Time.deltaTime;
                if (dirtyTimer <= 0f)
                {
                    ClearDirtyEffect();
                }
            }
        }

        private void SetupMirrorCameras()
        {
            if (leftMirrorCamera != null)
            {
                leftMirrorCamera.fieldOfView = mirrorFOV;
                leftMirrorCamera.farClipPlane = mirrorRange;
                leftMirrorCamera.targetTexture = leftMirrorTexture;
            }

            if (rightMirrorCamera != null)
            {
                rightMirrorCamera.fieldOfView = mirrorFOV;
                rightMirrorCamera.farClipPlane = mirrorRange;
                rightMirrorCamera.targetTexture = rightMirrorTexture;
            }
        }

        /// <summary>
        /// Activates the Dirty-Carrot effect, obscuring mirror vision.
        /// </summary>
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
