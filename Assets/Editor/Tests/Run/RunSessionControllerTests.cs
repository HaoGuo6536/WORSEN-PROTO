// ============================================================================
// RunSessionControllerTests.cs
// ============================================================================
//
// PURPOSE:
//   Verifies the run skeleton using explicit input, scene facts, and delta time.
//   These tests catch lost short presses, repeated button edges, premature ticks,
//   and stale input after a scene hand-off without starting Unity Play Mode.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Run.
//
// KEY RESPONSIBILITIES:
//   - Exercise valid and invalid phase transitions.
//   - Verify readiness, completion, timing, and restart boundaries.
//   - Replay input sequences and compare deterministic state and consumption.
//
// DEPENDENCIES:
//   - NUnit, pure Session Run types, Core input values, and Vector2 value math.
//
// USAGE NOTES:
//   Editor-only tests; no scene, input device, or Unity clock is required.
//   Random sources are explicitly seeded for each independently constructed run.
//
// ============================================================================

using System;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Run;

namespace Worsen.Tests.Run
{
    public sealed class RunSessionControllerTests
    {
        [TestCase(RunPhase.Boot, RunEvent.SceneReady, RunPhase.FirstSweep)]
        [TestCase(RunPhase.FirstSweep, RunEvent.ExitOpened, RunPhase.ExitOpen)]
        [TestCase(RunPhase.ExitOpen, RunEvent.CollapseStarted, RunPhase.Collapse)]
        [TestCase(RunPhase.FirstSweep, RunEvent.PlayerDied, RunPhase.Ended)]
        [TestCase(RunPhase.ExitOpen, RunEvent.PlayerDied, RunPhase.Ended)]
        [TestCase(RunPhase.Collapse, RunEvent.PlayerDied, RunPhase.Ended)]
        [TestCase(RunPhase.ExitOpen, RunEvent.ExitReached, RunPhase.Ended)]
        [TestCase(RunPhase.Collapse, RunEvent.ExitReached, RunPhase.Ended)]
        public void ExpectedFactsAdvanceTheRun(RunPhase current, RunEvent fact, RunPhase expected)
        {
            RunSessionController controller = Create(out _);
            Assert.That(controller.Next(current, fact), Is.EqualTo(expected));
        }

        [TestCase(RunPhase.Boot, RunEvent.ExitOpened)]
        [TestCase(RunPhase.Boot, RunEvent.PlayerDied)]
        [TestCase(RunPhase.FirstSweep, RunEvent.ExitReached)]
        [TestCase(RunPhase.FirstSweep, RunEvent.CollapseStarted)]
        [TestCase(RunPhase.ExitOpen, RunEvent.SceneReady)]
        [TestCase(RunPhase.Collapse, RunEvent.ExitOpened)]
        [TestCase(RunPhase.Ended, RunEvent.SceneReady)]
        public void OutOfOrderFactsDoNotSkipPhases(RunPhase current, RunEvent fact)
        {
            RunSessionController controller = Create(out _);
            Assert.That(controller.Next(current, fact), Is.EqualTo(current));
        }

        [Test]
        public void RunDoesNotTickOrBufferGameplayBeforeSceneReady()
        {
            RunSessionController controller = Create(out RunSessionBehaviorState state);
            controller.ReceiveInput(JumpFrame());

            Assert.That(controller.TryTick(1f / 60f, out _), Is.False);
            Assert.That(state.Tick, Is.Zero);
            Assert.That(state.Phase, Is.EqualTo(RunPhase.Boot));
            controller.StartScene(SceneKey.TagArena);
            Assert.That(controller.TryTick(1f / 60f, out InputFrame frame), Is.True);
            Assert.That(frame.Pressed, Is.EqualTo(InputButtons.None));
            Assert.That(state.Tick, Is.EqualTo(1));
        }

        [Test]
        public void ShortPressAndReleaseBetweenTicksSurviveAndAreConsumedOnce()
        {
            RunSessionController controller = Create(out _);
            controller.StartScene(SceneKey.TagArena);
            controller.ReceiveInput(new InputFrame(Vector2.up, new Vector2(2, 1),
                InputButtons.Jump, InputButtons.Jump, InputButtons.None));
            controller.ReceiveInput(new InputFrame(Vector2.right, new Vector2(3, -2),
                InputButtons.Sprint, InputButtons.Sprint, InputButtons.Jump));

            Assert.That(controller.TryTick(0.02f, out InputFrame frame), Is.True);
            Assert.That(frame.Move, Is.EqualTo(Vector2.right));
            Assert.That(frame.LookDelta, Is.EqualTo(new Vector2(5, -1)));
            Assert.That(frame.Held, Is.EqualTo(InputButtons.Sprint));
            Assert.That(frame.Pressed, Is.EqualTo(InputButtons.Jump | InputButtons.Sprint));
            Assert.That(frame.Released, Is.EqualTo(InputButtons.Jump));

            Assert.That(controller.TryTick(0.02f, out InputFrame next), Is.True);
            Assert.That(next.Move, Is.EqualTo(Vector2.right));
            Assert.That(next.Held, Is.EqualTo(InputButtons.Sprint));
            Assert.That(next.LookDelta, Is.EqualTo(Vector2.zero));
            Assert.That(next.Pressed, Is.EqualTo(InputButtons.None));
            Assert.That(next.Released, Is.EqualTo(InputButtons.None));
        }

        [Test]
        public void EndedRunDoesNotAdvanceOrCollectMoreInput()
        {
            RunSessionController controller = Create(out RunSessionBehaviorState state);
            controller.StartScene(SceneKey.TagArena);
            controller.TryTick(0.125f, out _);
            Assert.That(controller.Apply(RunEvent.PlayerDied), Is.EqualTo(RunPhase.Ended));
            controller.ReceiveInput(JumpFrame());

            Assert.That(controller.TryTick(0.125f, out _), Is.False);
            Assert.That(state.Tick, Is.EqualTo(1));
            Assert.That(state.ElapsedSeconds, Is.EqualTo(0.125d));
            Assert.That(state.PendingInput.Pressed, Is.EqualTo(InputButtons.None));
        }

        [Test]
        public void SceneHandoffResetsCountersAndDiscardsPriorSceneInput()
        {
            RunSessionController controller = Create(out RunSessionBehaviorState state);
            controller.StartScene(SceneKey.TagArena);
            controller.TryTick(0.5f, out _);
            controller.ReceiveInput(JumpFrame());
            controller.SuspendForSceneLoad();

            Assert.That(controller.TryTick(0.5f, out _), Is.False);
            controller.ReceiveInput(JumpFrame());
            controller.StartScene(SceneKey.TagArena);
            Assert.That(state.Seed, Is.EqualTo(1234));
            Assert.That(state.Scene, Is.EqualTo(SceneKey.TagArena));
            Assert.That(state.Phase, Is.EqualTo(RunPhase.FirstSweep));
            Assert.That(state.Tick, Is.Zero);
            Assert.That(state.ElapsedSeconds, Is.Zero);
            controller.TryTick(0.5f, out InputFrame frame);
            Assert.That(frame.Pressed, Is.EqualTo(InputButtons.None));
            Assert.That(frame.Held, Is.EqualTo(InputButtons.None));
        }

        [TestCase(0f)]
        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidTickDurationCannotCorruptTheCounter(float deltaTime)
        {
            RunSessionController controller = Create(out RunSessionBehaviorState state);
            controller.StartScene(SceneKey.TagArena);
            Assert.Throws<ArgumentOutOfRangeException>(() => controller.TryTick(deltaTime, out _));
            Assert.That(state.Tick, Is.Zero);
            Assert.That(state.ElapsedSeconds, Is.Zero);
        }

        [Test]
        public void RecordedFramesAndDeltasProduceIdenticalRuns()
        {
            RunSessionController first = Create(out RunSessionBehaviorState a);
            RunSessionController second = Create(out RunSessionBehaviorState b);
            first.StartScene(SceneKey.TagArena);
            second.StartScene(SceneKey.TagArena);
            var frames = new[]
            {
                JumpFrame(),
                new InputFrame(Vector2.left, new Vector2(-3, 2), InputButtons.LookBack,
                    InputButtons.LookBack, InputButtons.Jump),
                new InputFrame(Vector2.zero, Vector2.zero, InputButtons.None,
                    InputButtons.None, InputButtons.LookBack)
            };

            foreach (InputFrame recorded in frames)
            {
                first.ReceiveInput(recorded);
                second.ReceiveInput(recorded);
                first.TryTick(1f / 60f, out InputFrame firstFrame);
                second.TryTick(1f / 60f, out InputFrame secondFrame);
                Assert.That(secondFrame, Is.EqualTo(firstFrame));
                Assert.That(b.Tick, Is.EqualTo(a.Tick));
                Assert.That(b.ElapsedSeconds, Is.EqualTo(a.ElapsedSeconds));
                Assert.That(b.Phase, Is.EqualTo(a.Phase));
            }
        }

        [Test]
        public void InvalidSceneDoesNotDiscardTheCurrentRun()
        {
            RunSessionController controller = Create(out RunSessionBehaviorState state);
            controller.StartScene(SceneKey.TagArena);
            controller.TryTick(0.125f, out _);
            Assert.Throws<ArgumentOutOfRangeException>(() => controller.StartScene(SceneKey.None));
            Assert.That(state.Scene, Is.EqualTo(SceneKey.TagArena));
            Assert.That(state.Tick, Is.EqualTo(1));
        }

        private static RunSessionController Create(out RunSessionBehaviorState state)
        {
            state = new RunSessionBehaviorState(1234);
            return new RunSessionController(state, new System.Random(state.Seed));
        }

        private static InputFrame JumpFrame()
        {
            return new InputFrame(Vector2.up, Vector2.one,
                InputButtons.Jump, InputButtons.Jump, InputButtons.None);
        }
    }
}
