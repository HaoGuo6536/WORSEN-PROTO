// ============================================================================
// RunSessionBehaviorState.cs
// ============================================================================
//
// PURPOSE:
//   Stores the current run, fixed-step counter, and input waiting for a tick.
//   This data belongs to the persistent Run Session Manager and is changed only
//   by its Controller, keeping the run's rules out of the Unity component.
//
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Session · Run.
//
// KEY RESPONSIBILITIES:
//   - Retain the seed, phase, readiness, scene key, tick, and elapsed time.
//   - Hold pending input so button edges and look deltas survive between ticks.
//
// DEPENDENCIES:
//   - Core InputFrame, RunPhase, and SceneKey value types only.
//
// USAGE NOTES:
//   Persistent only through the owning Manager; this is not a Unity component.
//   It never retains scene objects or publishes events. No foreign system reads
//   this mutable state; the Manager exposes primitive properties and events.
//
// ============================================================================

using Worsen.Core;

namespace Worsen.Session.Run
{
    public sealed class RunSessionBehaviorState
    {
        public RunSessionBehaviorState(int seed)
        {
            Seed = seed;
        }

        public int Seed { get; }
        public RunPhase Phase { get; internal set; } = RunPhase.Boot;
        public SceneKey Scene { get; internal set; } = SceneKey.None;
        public bool SceneIsReady { get; internal set; }
        public long Tick { get; internal set; }
        public double ElapsedSeconds { get; internal set; }
        public InputFrame PendingInput { get; internal set; }
    }
}
