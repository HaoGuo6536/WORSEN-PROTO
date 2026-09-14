// ============================================================================
// RunSessionController.cs
// ============================================================================
//
// PURPOSE:
//   Advances the solo run from explicit facts and consumes one input frame per
//   fixed tick. It accumulates input between ticks and clears one-shot data after
//   consumption so render timing cannot duplicate a jump or lose a short press.
//
// ARCHITECTURAL ROLE:
//   Controller (§2) · Session · Run.
//   Owns the rules over RunSessionBehaviorState without touching a Unity scene.
//
// KEY RESPONSIBILITIES:
//   - Define phase transitions and gate ticks on scene readiness and run state.
//   - Combine pending input, retain held controls, and consume edges exactly once.
//   - Advance the run counter from explicit positive delta time values.
//
// DEPENDENCIES:
//   - Run state and definitions in this system; Core input and phase values.
//   - Injected System.Random, shared with future run-owned gameplay consumers.
//
// USAGE NOTES:
//   StartScene is a new scene-instance hand-off: it resets counters and pending
//   input. The Manager supplies a fresh random source with the same seed for a
//   restarted scene. Time and randomness never come from static engine APIs.
//
// ============================================================================

using System;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Session.Run
{
    public sealed class RunSessionController
    {
        private readonly RunSessionBehaviorState state;

        public RunSessionController(RunSessionBehaviorState state, System.Random random)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            RandomSource = random ?? throw new ArgumentNullException(nameof(random));
        }

        public System.Random RandomSource { get; }

        public RunPhase Next(RunPhase current, RunEvent fact)
        {
            switch (current)
            {
                case RunPhase.Boot:
                    return fact == RunEvent.SceneReady ? RunPhase.FirstSweep : current;
                case RunPhase.FirstSweep:
                    if (fact == RunEvent.PlayerDied) return RunPhase.Ended;
                    return fact == RunEvent.ExitOpened ? RunPhase.ExitOpen : current;
                case RunPhase.ExitOpen:
                    if (fact == RunEvent.PlayerDied || fact == RunEvent.ExitReached) return RunPhase.Ended;
                    return fact == RunEvent.CollapseStarted ? RunPhase.Collapse : current;
                case RunPhase.Collapse:
                    return fact == RunEvent.PlayerDied || fact == RunEvent.ExitReached ? RunPhase.Ended : current;
                default:
                    return current;
            }
        }

        public void StartScene(SceneKey scene)
        {
            if (scene != SceneKey.TagArena && scene != SceneKey.FloorLoop)
                throw new ArgumentOutOfRangeException(nameof(scene), scene, "A run needs a supported gameplay scene.");

            state.Scene = scene;
            state.SceneIsReady = true;
            state.Phase = Next(RunPhase.Boot, RunEvent.SceneReady);
            state.Tick = 0;
            state.ElapsedSeconds = 0;
            state.PendingInput = default;
        }

        public void SuspendForSceneLoad()
        {
            state.SceneIsReady = false;
            state.PendingInput = default;
        }

        public RunPhase Apply(RunEvent fact)
        {
            state.Phase = Next(state.Phase, fact);
            return state.Phase;
        }

        public void ReceiveInput(InputFrame frame)
        {
            if (!state.SceneIsReady || state.Phase == RunPhase.Ended) return;

            InputFrame pending = state.PendingInput;
            state.PendingInput = new InputFrame(frame.Move, pending.LookDelta + frame.LookDelta,
                frame.Held, pending.Pressed | frame.Pressed, pending.Released | frame.Released);
        }

        public bool TryTick(float deltaTime, out InputFrame frame)
        {
            frame = default;
            if (!state.SceneIsReady || state.Phase == RunPhase.Boot || state.Phase == RunPhase.Ended)
                return false;
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Tick duration must be finite and positive.");

            frame = state.PendingInput;
            state.PendingInput = new InputFrame(frame.Move, Vector2.zero, frame.Held, InputButtons.None, InputButtons.None);
            state.Tick++;
            state.ElapsedSeconds += deltaTime;
            return true;
        }
    }
}
