using UnityEngine;

namespace ReverseRabbitRunner.Player
{
    /// <summary>
    /// Smooth camera follow positioned to see the rabbit's FACE and the angry farmer beyond.
    /// The rabbit runs in -Z but faces +Z, so the camera sits at -Z offset looking towards +Z.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0, 3.5f, -10f);
        [SerializeField] private float smoothSpeed = 8f;

        [Header("Dynamic")]
        [SerializeField] private float speedZoomFactor = 0.02f;
        [SerializeField] private float maxFOVIncrease = 15f;

        private Camera cam;
        private float baseFOV;

        private void Start()
        {
            cam = GetComponent<Camera>();
            if (cam != null) baseFOV = cam.fieldOfView;

            if (target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Position camera at -Z offset (behind the rabbit's face direction)
            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

            // Look at the rabbit (and farmer beyond it in +Z direction)
            Vector3 lookTarget = target.position + new Vector3(0, 0.5f, 5f);
            transform.LookAt(lookTarget);

            // Speed-based FOV increase
            if (cam != null)
            {
                var rabbit = target.GetComponent<RabbitController>();
                if (rabbit != null)
                {
                    float speedFOV = rabbit.CurrentSpeed * speedZoomFactor;
                    cam.fieldOfView = Mathf.Lerp(cam.fieldOfView,
                        baseFOV + Mathf.Min(speedFOV, maxFOVIncrease),
                        2f * Time.deltaTime);
                }
            }
        }
    }
}
