using UnityEngine;

namespace ReverseRabbitRunner.PowerUps
{
    /// <summary>
    /// Base class for all special carrot power-ups.
    /// </summary>
    public abstract class PowerUpBase : MonoBehaviour
    {
        [Header("Power-Up Settings")]
        [SerializeField] protected float duration = 5f;
        [SerializeField] protected float rotationSpeed = 90f;

        protected bool isActive = false;
        protected float timer = 0f;

        protected virtual void Update()
        {
            // Visual spin effect
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Called when the rabbit collects this power-up.
        /// </summary>
        public void Collect(Player.RabbitController rabbit)
        {
            Activate(rabbit);
            // Hide the pickup but keep it alive to manage the effect duration
            GetComponent<Renderer>().enabled = false;
            GetComponent<Collider>().enabled = false;
        }

        protected abstract void Activate(Player.RabbitController rabbit);
        protected abstract void Deactivate(Player.RabbitController rabbit);
    }
}
