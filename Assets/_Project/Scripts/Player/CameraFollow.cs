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

        [Header("Flight Mode")]
        [SerializeField] private Vector3 flightOffset = new Vector3(0, 5f, 10f);
        [SerializeField] private Vector3 flightLookOffset = new Vector3(0, 0.5f, -5f);

        [Header("Dynamic")]
        [SerializeField] private float speedZoomFactor = 0.02f;
        [SerializeField] private float maxFOVIncrease = 15f;

        private Camera cam;
        private float baseFOV;
        private bool isInFlightMode;
        private Vector3 normalLookOffset = new Vector3(0, 0.5f, 5f);

        public void SetFlightMode(bool flight) => isInFlightMode = flight;

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

            // Position camera — swap offset during flight to see path ahead
            Vector3 currentOffset = isInFlightMode ? flightOffset : offset;
            Vector3 desiredPosition = target.position + currentOffset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

            // Look target — forward during flight, backward (farmer) normally
            Vector3 lookOffset = isInFlightMode ? flightLookOffset : normalLookOffset;
            Vector3 lookTarget = target.position + lookOffset;
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
