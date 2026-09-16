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
//   - Admit the runtime-generated horror scene to the same fixed-step lifecycle.
//   - Define phase transitions and gate ticks on scene readiness and run state.
//   - Combine pending input, retain held controls, and consume edges exactly once.
//   - Advance the run counter from explicit positive delta time values.
//   - Close observational capture exactly once across repeated shutdown facts.
//   - Accumulate run outcomes and confirmed-chase statistics from committed facts.
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
using EntityId = Worsen.Core.EntityId;

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
            if (scene != SceneKey.TagArena && scene != SceneKey.FloorLoop && scene != SceneKey.HorrorRun)
                throw new ArgumentOutOfRangeException(nameof(scene), scene, "A run needs a supported gameplay scene.");

            state.Scene = scene;
            state.SceneIsReady = true;
            state.Phase = Next(RunPhase.Boot, RunEvent.SceneReady);
            state.Tick = 0;
            state.ElapsedSeconds = 0;
            state.PendingInput = default;
            state.CakesCollected = state.GoldenCakesCollected = 0;
            state.ChaseCount = state.ChasesEscaped = state.ActiveChaseId = 0;
            state.ChaseStartedAt = state.TotalChaseSeconds = 0;
            state.PendingEndReason = RunEndReason.Unknown;
            state.DeadPlayer = EntityId.None;
            state.KillerPosition = Vector3.zero;
            state.TelemetryEventId = 0;
        }

        public void RecordCollection(PickupCollectedFact fact)
        {
            if (state.Phase == RunPhase.Ended) return;
            state.CakesCollected = fact.CakeCount;
            state.GoldenCakesCollected = fact.GoldenCount;
        }

        public void RecordChaseStarted(ChaseFact fact)
        {
            if (fact.ChaseId <= 0 || state.ActiveChaseId == fact.ChaseId || state.Phase == RunPhase.Ended) return;
            CloseChase();
            state.ActiveChaseId = fact.ChaseId;
            state.ChaseStartedAt = state.ElapsedSeconds;
            state.ChaseCount++;
        }

        public void RecordChaseEnded(ChaseFact fact)
        {
            if (state.ActiveChaseId != fact.ChaseId || fact.ChaseId <= 0) return;
            if (fact.EndReason == ChaseEndReason.Lost) state.ChasesEscaped++;
            CloseChase();
        }

        private void CloseChase()
        {
            if (state.ActiveChaseId > 0)
                state.TotalChaseSeconds += Math.Max(0, state.ElapsedSeconds - state.ChaseStartedAt);
            state.ActiveChaseId = 0;
        }

        public void RequestEnd(RunEndReason reason, EntityId player, Vector3 killerPosition)
        {
            if (state.Phase == RunPhase.Ended || reason == RunEndReason.Unknown) return;
            if (reason == RunEndReason.Escaped && state.Phase != RunPhase.ExitOpen && state.Phase != RunPhase.Collapse) return;
            if (state.PendingEndReason == RunEndReason.Died) return;
            state.PendingEndReason = reason;
            if (reason == RunEndReason.Died) { state.DeadPlayer = player; state.KillerPosition = killerPosition; }
        }

        public bool TryFinish(out RunSummary summary)
        {
            summary = default;
            if (state.Phase == RunPhase.Ended || state.PendingEndReason == RunEndReason.Unknown) return false;
            CloseChase();
            state.Phase = RunPhase.Ended;
            state.PendingInput = default;
            summary = new RunSummary(state.ElapsedSeconds, state.CakesCollected, state.GoldenCakesCollected,
                state.ChaseCount, state.ChasesEscaped, state.TotalChaseSeconds, state.PendingEndReason, state.Seed, state.Scene);
            return true;
        }

        public long NextTelemetryEventId() => ++state.TelemetryEventId;

        public float NormalizeSpeed(Vector3 velocity, float maximum)
        {
            if (!(maximum > 0) || float.IsInfinity(maximum) || float.IsNaN(maximum)) return 0;
            double horizontal = Math.Sqrt((double)velocity.x * velocity.x + (double)velocity.z * velocity.z);
            return double.IsNaN(horizontal) || double.IsInfinity(horizontal) ? 0 : (float)Math.Min(1, horizontal / maximum);
        }

        public int EmptySlots(InventorySnapshot inventory) =>
            (string.IsNullOrEmpty(inventory.SlotOne) ? 1 : 0) + (string.IsNullOrEmpty(inventory.SlotTwo) ? 1 : 0);

        public void ConfigureCapture(string revision, string configHash)
        {
            state.SourceRevision = revision ?? string.Empty;
            state.ConfigSnapshotHash = configHash ?? string.Empty;
        }

        public void OpenCapture() => state.CaptureIsOpen = true;

        public bool TryCloseCapture()
        {
            if (!state.CaptureIsOpen) return false;
            state.CaptureIsOpen = false;
            return true;
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

