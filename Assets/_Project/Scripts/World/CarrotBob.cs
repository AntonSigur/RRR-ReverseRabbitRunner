using UnityEngine;

namespace ReverseRabbitRunner.World
{
    /// <summary>
    /// Makes carrots gently bob up and down to look alive and be easier to spot.
    /// Each carrot gets a random phase offset so they don't all move in sync.
    /// </summary>
    public class CarrotBob : MonoBehaviour
    {
        [SerializeField] private float bobHeight = 0.15f;
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float rotateSpeed = 45f;

        private float baseY;
        private float phaseOffset;

        private void Start()
        {
            baseY = transform.position.y;
            phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            float y = baseY + Mathf.Sin(Time.time * bobSpeed + phaseOffset) * bobHeight;
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        }
    }
}
