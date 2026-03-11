using UnityEngine;

namespace ReverseRabbitRunner.Enemies
{
    /// <summary>
    /// Animates the farmer's pitchfork with periodic angry waves.
    /// Attach to the farmer parent — finds ForkPivot automatically.
    /// </summary>
    public class FarmerForkWave : MonoBehaviour
    {
        [SerializeField] private float waveCooldownMin = 2f;
        [SerializeField] private float waveCooldownMax = 5f;
        [SerializeField] private float waveDuration = 0.8f;
        [SerializeField] private float waveAngle = 50f;

        private Transform forkPivot;
        private Quaternion baseRotation;
        private float cooldownTimer;
        private float waveTimer;
        private bool isWaving;

        private void Start()
        {
            forkPivot = transform.Find("ForkPivot");
            if (forkPivot != null)
                baseRotation = forkPivot.localRotation;
            cooldownTimer = Random.Range(waveCooldownMin, waveCooldownMax);
        }

        private void Update()
        {
            if (forkPivot == null) return;

            if (isWaving)
            {
                waveTimer += Time.deltaTime;
                float t = waveTimer / waveDuration;
                if (t >= 1f)
                {
                    isWaving = false;
                    forkPivot.localRotation = baseRotation;
                    cooldownTimer = Random.Range(waveCooldownMin, waveCooldownMax);
                }
                else
                {
                    // Quick raise then return: sin curve for smooth wave
                    float angle = Mathf.Sin(t * Mathf.PI) * waveAngle;
                    forkPivot.localRotation = baseRotation * Quaternion.Euler(angle, 0, angle * 0.3f);
                }
            }
            else
            {
                cooldownTimer -= Time.deltaTime;
                if (cooldownTimer <= 0f)
                {
                    isWaving = true;
                    waveTimer = 0f;
                }
            }
        }
    }
}
