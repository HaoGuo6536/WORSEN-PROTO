// ============================================================================
// RunConsumableTests.cs
// ============================================================================
// PURPOSE:
//   Verifies consumable input survives the Run accumulator once per press.
//   Revival must preserve the existing collapse phase, elapsed time and capture.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · Run.
// KEY RESPONSIBILITIES:
//   - Cover additive input bits, variable slot counts and reversible pending death.
// DEPENDENCIES:
//   - Core input, Run pure controller/state and NUnit.
// USAGE NOTES:
//   Edit Mode; no scene assembly or engine clock.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Run;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Run
{
    public sealed class RunConsumableTests
    {
        [TestCase(InputButtons.UseConsumable)] [TestCase(InputButtons.CycleConsumable)] [TestCase(InputButtons.CycleConsumablePrevious)]
        public void ItemEdgesAreForwardedOnceWithoutStoppingMovement(InputButtons button)
        {
            var state = new RunSessionBehaviorState(1); var run = new RunSessionController(state, new System.Random(1)); run.StartScene(SceneKey.HorrorRun);
            run.ReceiveInput(new InputFrame(Vector2.up, Vector2.zero, button, button, InputButtons.None));
            Assert.That(run.TryTick(.02f, out var first), Is.True); Assert.That(first.Pressed, Is.EqualTo(button));
            Assert.That(first.Move, Is.EqualTo(Vector2.up));
            Assert.That(run.TryTick(.02f, out var second), Is.True); Assert.That(second.Pressed, Is.EqualTo(InputButtons.None));
            Assert.That(second.Held, Is.EqualTo(button)); Assert.That(second.Move, Is.EqualTo(Vector2.up));
        }
        [Test]
        public void RevivalCancelsOnlyItsDeathAndLeavesCollapseTimeAndCaptureRunning()
        {
            var state = new RunSessionBehaviorState(1); var run = new RunSessionController(state, new System.Random(1)); run.StartScene(SceneKey.HorrorRun);
            run.OpenCapture(); run.Apply(RunEvent.ExitOpened); run.Apply(RunEvent.CollapseStarted); run.TryTick(1f, out _);
            var player = new EntityId(1); run.RequestEnd(RunEndReason.Died, player, Vector3.forward);
            Assert.That(run.CancelDeathForRevival(new EntityId(2)), Is.False);
            Assert.That(run.CancelDeathForRevival(player), Is.True); Assert.That(run.TryFinish(out _), Is.False);
            Assert.That(state.Phase, Is.EqualTo(RunPhase.Collapse)); Assert.That(state.CaptureIsOpen, Is.True);
            Assert.That(run.TryTick(1f, out _), Is.True); Assert.That(state.ElapsedSeconds, Is.EqualTo(2d));
            run.RequestEnd(RunEndReason.Died, player, Vector3.forward);
            Assert.That(run.TryFinish(out var summary), Is.True); Assert.That(summary.EndReason, Is.EqualTo(RunEndReason.Died));
            Assert.That(run.CancelDeathForRevival(player), Is.False);
        }
        [Test]
        public void EmptySlotCountUsesTheWholeProgressionInventory()
        {
            var run = new RunSessionController(new RunSessionBehaviorState(1), new System.Random(1));
            var slots = new[] { default(ProgressionInventorySlot), new ProgressionInventorySlot("gauze", "Gauze", 4), default, default };
            Assert.That(run.EmptySlots(slots), Is.EqualTo(3));
        }
    }
}
