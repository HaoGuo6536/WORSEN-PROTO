// ============================================================================
// RunOutcomeTests.cs
// ============================================================================
// PURPOSE:
//   Verifies committed run outcomes, chase statistics and terminal precedence.
//   Pure fixtures isolate timing and duplicate events from Unity callback order.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Run.
// KEY RESPONSIBILITIES:
//   - Project empty capacity from Progression's variable-length slots, not Player's legacy pair.
//   - Verify confirmed chase durations, restart reset and exactly-once completion.
//   - Reject early escapes, preserve death priority and reset pending outcomes.
// DEPENDENCIES:
//   - Session Run Controller/state, Core facts and NUnit assertions.
// USAGE NOTES:
//   These checks do not establish live scene wiring or human acceptance.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
using Worsen.Session.Run;

namespace Worsen.Tests.Run
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class RunOutcomeTests
    {
        private RunSessionBehaviorState state;
        private RunSessionController controller;
        private readonly EntityId player = new EntityId(1);
        private readonly EntityId hunter = new EntityId(-1);
        [SetUp]
        public void SetUp()
        {
            state = new RunSessionBehaviorState(42);
            controller = new RunSessionController(state, new System.Random(42));
            controller.StartScene(SceneKey.FloorLoop);
        }
        [Test]
        public void FirstSweepCannotEscapeAndNormalEscapeCompletesOnce()
        {
            controller.RequestEnd(RunEndReason.Escaped, player, Vector3.zero);
            Assert.That(controller.TryFinish(out _), Is.False);
            controller.Apply(RunEvent.ExitOpened);
            controller.RequestEnd(RunEndReason.Escaped, player, Vector3.zero);
            Assert.That(controller.TryFinish(out var summary), Is.True);
            Assert.That(summary.EndReason, Is.EqualTo(RunEndReason.Escaped));
            Assert.That(summary.Bailed, Is.False);
            Assert.That(controller.TryFinish(out _), Is.False);
            controller.StartScene(SceneKey.HorrorRun);
            controller.RequestEnd(RunEndReason.Escaped, player, Vector3.zero);
            Assert.That(controller.TryFinish(out _), Is.False);
            controller.Apply(RunEvent.ExitOpened);
            controller.RequestEnd(RunEndReason.Escaped, player, Vector3.zero);
            Assert.That(controller.TryFinish(out summary), Is.True);
            Assert.That(summary.Bailed, Is.False);
            Assert.That(new RunSummary(0, 0, 0, 0, 0, 0, RunEndReason.Escaped).Bailed, Is.False);
        }
        [TestCase(false)]
        [TestCase(true)]
        public void DeathWinsPrematureEscapeInEitherOrder(bool deathFirst)
        {
            if (deathFirst) controller.RequestEnd(RunEndReason.Died, player, Vector3.right);
            controller.RequestEnd(RunEndReason.Escaped, player, Vector3.zero);
            if (!deathFirst) controller.RequestEnd(RunEndReason.Died, player, Vector3.right);
            Assert.That(controller.TryFinish(out var summary), Is.True);
            Assert.That(summary.EndReason, Is.EqualTo(RunEndReason.Died));
            Assert.That(summary.Bailed, Is.False);
            Assert.That(state.KillerPosition, Is.EqualTo(Vector3.right));
        }
        [Test]
        public void BootCannotEscapeAndPendingCompletionClearsOnSceneReset()
        {
            var boot = new RunSessionController(new RunSessionBehaviorState(1), new System.Random(1));
            boot.RequestEnd(RunEndReason.Escaped, player, Vector3.zero);
            Assert.That(boot.TryFinish(out _), Is.False);
            controller.Apply(RunEvent.ExitOpened);
            controller.RequestEnd(RunEndReason.Escaped, player, Vector3.zero);
            controller.StartScene(SceneKey.HorrorRun);
            Assert.That(state.PendingEndReason, Is.EqualTo(RunEndReason.Unknown));
            Assert.That(controller.TryFinish(out _), Is.False);
        }
        private void Advance(float seconds) => controller.TryTick(seconds, out _);
        private ChaseFact Fact(int id, ChaseEndReason reason = ChaseEndReason.Unknown) =>
            new ChaseFact(id, player, hunter, state.Tick, reason == ChaseEndReason.Unknown ? ChasePhase.Confirmed : ChasePhase.None, reason);
        [Test]
        public void ConfirmedChaseCountsOnceAndMeasuresOnlyItsElapsedWindow()
        {
            Advance(2); controller.RecordChaseStarted(Fact(1)); controller.RecordChaseStarted(Fact(1));
            Advance(12); controller.RecordChaseEnded(Fact(1, ChaseEndReason.Lost));
            controller.RecordChaseEnded(Fact(1, ChaseEndReason.Lost));
            controller.Apply(RunEvent.ExitOpened); controller.RequestEnd(RunEndReason.Escaped, player, Vector3.zero);
            Assert.That(controller.TryFinish(out var summary), Is.True);
            Assert.That(summary.ElapsedSeconds, Is.EqualTo(14));
            Assert.That(summary.ChaseCount, Is.EqualTo(1));
            Assert.That(summary.ChasesEscaped, Is.EqualTo(1));
            Assert.That(summary.TotalChaseSeconds, Is.EqualTo(12));
            Assert.That(summary.Seed, Is.EqualTo(42));
            Assert.That(controller.TryFinish(out _), Is.False);
        }
        [Test]
        public void DeathWinsExitWhenBothFactsArriveBeforeCommit()
        {
            controller.Apply(RunEvent.ExitOpened);
            controller.RequestEnd(RunEndReason.Escaped, player, Vector3.zero);
            controller.RequestEnd(RunEndReason.Died, player, Vector3.right);
            controller.RequestEnd(RunEndReason.Escaped, player, Vector3.zero);
            Assert.That(controller.TryFinish(out var summary), Is.True);
            Assert.That(summary.EndReason, Is.EqualTo(RunEndReason.Died));
            Assert.That(state.KillerPosition, Is.EqualTo(Vector3.right));
        }
        [Test]
        public void LockedExitCannotCompleteTheRun()
        {
            controller.RequestEnd(RunEndReason.Escaped, player, Vector3.zero);
            Assert.That(controller.TryFinish(out _), Is.False);
        }
        [Test]
        public void RestartResetsCountsDurationsAndPendingTerminalFact()
        {
            controller.RecordCollection(new PickupCollectedFact(player, 301, PickupKind.Cake, 10, 2, 1));
            controller.RecordChaseStarted(Fact(1)); Advance(7);
            controller.RecordChaseEnded(Fact(1, ChaseEndReason.Lost));
            Assert.That(state.TotalChaseSeconds, Is.EqualTo(7));
            Assert.That(state.ChasesEscaped, Is.EqualTo(1));
            controller.RequestEnd(RunEndReason.Died, player, Vector3.right);
            controller.StartScene(SceneKey.TagArena);
            Assert.That(controller.TryFinish(out _), Is.False);
            Assert.That(state.ChaseCount, Is.Zero);
            Assert.That(state.CakesCollected, Is.Zero);
            Assert.That(state.GoldenCakesCollected, Is.Zero);
            Assert.That(state.ElapsedSeconds, Is.Zero);
            Assert.That(state.TotalChaseSeconds, Is.Zero);
            Assert.That(state.ChasesEscaped, Is.Zero);
        }
        [Test]
        public void PreconfirmationCatchDoesNotCreateConfirmedChaseStatistics()
        {
            controller.RecordChaseEnded(Fact(0, ChaseEndReason.Lunge));
            controller.RequestEnd(RunEndReason.Died, player, Vector3.zero);
            Assert.That(controller.TryFinish(out var summary), Is.True);
            Assert.That(summary.ChaseCount, Is.Zero);
            Assert.That(summary.TotalChaseSeconds, Is.Zero);
        }
        [Test]
        public void EndingAnActiveChaseIncludesDurationWithoutClaimingAnEscape()
        {
            controller.RecordChaseStarted(Fact(2)); Advance(5);
            controller.RequestEnd(RunEndReason.Died, player, Vector3.zero);
            Assert.That(controller.TryFinish(out var summary), Is.True);
            Assert.That(summary.TotalChaseSeconds, Is.EqualTo(5));
            Assert.That(summary.ChasesEscaped, Is.Zero);
        }
        [Test]
        public void PresentationProjectionUsesHorizontalSpeedAndActualEmptySlots()
        {
            Assert.That(controller.NormalizeSpeed(new Vector3(3, 100, 4), 10), Is.EqualTo(0.5f));
            Assert.That(controller.NormalizeSpeed(new Vector3(30, 0, 40), 10), Is.EqualTo(1));
            Assert.That(controller.NormalizeSpeed(Vector3.one, 0), Is.Zero);
            Assert.That(controller.NormalizeSpeed(new Vector3(float.NaN, 0, 0), 10), Is.Zero);
            Assert.That(controller.EmptySlots(default), Is.Zero);
            Assert.That(controller.EmptySlots(new[] { new ProgressionInventorySlot("reserved", "Reserved", 0), default, default }), Is.EqualTo(2));
        }
    }
}

