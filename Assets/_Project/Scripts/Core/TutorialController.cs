using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Drives the <see cref="Tutorial"/> state machine: starts it on the
    /// first run, watches <see cref="Player.RabbitController"/> for lane
    /// swaps / jumps / carrot pickups, and ends the tutorial after the time
    /// limit. Auto-spawned via <see cref="RuntimeInitializeOnLoadMethodAttribute"/>
    /// so the gameplay scene needs no manual wiring.
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        public static TutorialController Instance { get; private set; }

        private Player.RabbitController rabbit;
        private int prevLane = -1;
        private bool prevJumping;
        private bool subscribedCarrot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[TutorialController]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<TutorialController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            // Acquire the rabbit lazily; reset cached state when scene reloads
            // (rabbit destroyed and replaced on R-restart).
            if (rabbit == null)
            {
                rabbit = FindAnyObjectByType<Player.RabbitController>();
                if (rabbit == null)
                {
                    Detach();
                    return;
                }

                prevLane = rabbit.CurrentLane;
                prevJumping = rabbit.IsJumping;
                if (!subscribedCarrot)
                {
                    rabbit.OnCollectCarrot += OnCarrot;
                    subscribedCarrot = true;
                }

                // Decide whether to start the tutorial when the player begins
                // a fresh run. We trigger on Playing state to avoid firing
                // while the menu / death cinematic owns the scene.
                var gm = GameManager.Instance;
                if (gm != null && gm.CurrentState == GameManager.GameState.Playing
                    && Tutorial.IsFirstRun && !Tutorial.IsActive
                    && Tutorial.Current == Tutorial.Step.Done)
                {
                    Tutorial.Begin();
                }
            }

            if (!Tutorial.IsActive) return;

            // Step detection — lane swap, jump rising edge.
            if (rabbit.CurrentLane != prevLane)
            {
                prevLane = rabbit.CurrentLane;
                Tutorial.Advance(Tutorial.Step.LaneSwitch);
            }
            if (rabbit.IsJumping && !prevJumping)
                Tutorial.Advance(Tutorial.Step.Jump);
            prevJumping = rabbit.IsJumping;

            // Hard timeout.
            if (Time.time - Tutorial.StartedAt > Tutorial.MaxDuration)
                Tutorial.End();

            // End tutorial if the run ended (death cinematic etc.).
            var gmNow = GameManager.Instance;
            if (gmNow != null && gmNow.CurrentState != GameManager.GameState.Playing)
                Tutorial.End();
        }

        private void OnCarrot(GameObject _)
        {
            Tutorial.Advance(Tutorial.Step.Carrots);
        }

        private void Detach()
        {
            if (subscribedCarrot && rabbit != null)
                rabbit.OnCollectCarrot -= OnCarrot;
            subscribedCarrot = false;
            rabbit = null;
            prevLane = -1;
            prevJumping = false;
        }

        private void OnDestroy()
        {
            Detach();
            if (Instance == this) Instance = null;
        }
    }
}
