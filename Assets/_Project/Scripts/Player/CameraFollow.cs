using UnityEngine;

namespace ReverseRabbitRunner.Player
{
    /// <summary>
    /// Smooth camera follow that tracks the rabbit from behind.
    /// Since the rabbit runs backwards (-Z), the camera follows at +Z offset.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0, 3f, 8f);
        [SerializeField] private float smoothSpeed = 8f;
        [SerializeField] private float lookAheadDistance = 5f;

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

            // Camera position: behind and above the rabbit
            // The rabbit runs in -Z, so camera is at +Z offset (behind the running direction)
            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

            // Look at a point slightly behind the rabbit (ahead in running direction)
            Vector3 lookTarget = target.position + Vector3.forward * lookAheadDistance;
            lookTarget.y = target.position.y + 0.5f;
            transform.LookAt(lookTarget);

            // Speed-based FOV increase for sense of velocity
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
