// ============================================================================
// RunSessionManager.cs
// ============================================================================
//
// PURPOSE:
//   Owns the prototype's single fixed tick and reproducible run seed. A SceneRoot
//   initializes this service explicitly and signals readiness before any tick,
//   so gameplay never depends on which Unity component happened to awaken first.
//
// ARCHITECTURAL ROLE:
//   Manager (§1) · Session · Run (Session system).
//   Owns the pure Controller and BehaviorState; publishes Core-typed run facts.
//
// KEY RESPONSIBILITIES:
//   - Maintain one persistent canonical run and one shared seeded random source.
//   - Request synchronous input publication immediately before each fixed tick.
//   - Hand explicit delta time to the Controller and publish completed tick data.
//
// DEPENDENCIES:
//   - Run Controller, state, and definitions in this system; shared Core types.
//   - Unity lifecycle and fixed delta time at the Session engine boundary (§8b).
//
// USAGE NOTES:
//   Persistent: this component is installed on its own root GameObject by the
//   setup tool. Initialize returns the canonical instance; duplicate components
//   destroy themselves without changing that instance's seed or subscriptions.
//   A SceneRoot sends HandleSceneReady once for each assembled scene instance,
//   which starts a fresh run using the retained seed. No scene objects are cached.
//   M0 publishes ticks but has no Player, Hunter, or floor simulation registered.
//
// ============================================================================

using System;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Session.Run
{
    public sealed class RunSessionManager : MonoBehaviour
    {
        private RunSessionBehaviorState state;
        private RunSessionController controller;

        public static RunSessionManager Instance { get; private set; }

        public event Action BeforeTick;
        public event Action<InputFrame, float, long> TickAdvanced;
        public event Action<RunPhase> PhaseChanged;

        public RunPhase Phase => state == null ? RunPhase.Boot : state.Phase;
        public long Tick => state == null ? 0 : state.Tick;
        public int Seed => state == null ? 0 : state.Seed;
        public double ElapsedSeconds => state == null ? 0 : state.ElapsedSeconds;
        public System.Random RandomSource => controller == null ? null : controller.RandomSource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        public RunSessionManager Initialize(int seed)
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return Instance;
            }

            Instance = this;
            if (controller == null)
            {
                state = new RunSessionBehaviorState(seed);
                controller = new RunSessionController(state, new System.Random(seed));
            }
            DontDestroyOnLoad(gameObject);
            return this;
        }

        public void ReceiveInput(InputFrame frame)
        {
            if (controller != null) controller.ReceiveInput(frame);
        }

        public void HandleSceneReady(SceneKey scene)
        {
            if (controller == null)
                throw new InvalidOperationException("Initialize the Run Session before announcing scene readiness.");

            controller = new RunSessionController(state, new System.Random(state.Seed));
            controller.StartScene(scene);
            PhaseChanged?.Invoke(state.Phase);
        }

        public void SuspendForSceneLoad()
        {
            if (controller != null) controller.SuspendForSceneLoad();
        }

        private void FixedUpdate()
        {
            if (Instance != this || state == null || !state.SceneIsReady || state.Phase == RunPhase.Ended)
                return;

            BeforeTick?.Invoke();
            float deltaTime = Time.fixedDeltaTime;
            if (controller.TryTick(deltaTime, out InputFrame frame))
                TickAdvanced?.Invoke(frame, deltaTime, state.Tick);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            BeforeTick = null;
            TickAdvanced = null;
            PhaseChanged = null;
            controller = null;
            state = null;
        }
    }
}
