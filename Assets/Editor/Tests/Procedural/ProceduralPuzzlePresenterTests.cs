// ============================================================================
// ProceduralPuzzlePresenterTests.cs
// ============================================================================
// PURPOSE:
//   Exercises all execution rules without a scene, physics or an engine clock.
//   Solutions and failures use explicit time so a render-rate difference cannot
//   turn waiting, repeated contact or an unrelated traversal into a solution.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Verify sequence order, expiry, reset, motion gating and exactly-once facts.
// DEPENDENCIES:
//   - NUnit and Domain.Procedural only.
// USAGE NOTES:
//   No Unity object creation; also suitable for the offline rule smoke harness.
// ============================================================================
using System;
using NUnit.Framework;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    public sealed class ProceduralPuzzlePresenterTests
    {
        private readonly ProceduralPuzzlePresenter _rules = new ProceduralPuzzlePresenter();
        private bool Step(ProceduralPuzzleDriverState s, ProceduralPuzzleKind k, int tile, float dt = 0.1f,
            bool contact = true, bool vault = false, float speed = 3f, bool near = true)
            => _rules.Step(s, k, dt, tile, speed, near, 6f, 1.5f, 2f, contact, vault);

        [TestCase(ProceduralPuzzleKind.OrderedPlates)] [TestCase(ProceduralPuzzleKind.DimmingPath)]
        [TestCase(ProceduralPuzzleKind.TimedVaults)]
        public void IntendedMovementSolvesExactlyOnce(ProceduralPuzzleKind kind)
        {
            var s = new ProceduralPuzzleDriverState();
            Assert.That(Step(s, kind, 0, vault: true), Is.False);
            Assert.That(Step(s, kind, 1, vault: true), Is.False);
            Assert.That(Step(s, kind, 2, vault: true), Is.True);
            Assert.That(s.GateOpen, Is.True);
            Assert.That(Step(s, kind, 2, vault: true), Is.False);
        }
        [TestCase(ProceduralPuzzleKind.OrderedPlates)] [TestCase(ProceduralPuzzleKind.DimmingPath)]
        [TestCase(ProceduralPuzzleKind.TimedVaults)]
        public void WrongOrderResetsAndFirstStepCanRestart(ProceduralPuzzleKind kind)
        {
            var s = new ProceduralPuzzleDriverState();
            Step(s, kind, 0, vault: true); Step(s, kind, 2, vault: true);
            Assert.That(s.Next, Is.Zero); Assert.That(s.Solved, Is.False);
            Step(s, kind, 0, vault: true); Step(s, kind, 1, vault: true);
            Assert.That(Step(s, kind, 2, vault: true), Is.True);
        }
        [TestCase(ProceduralPuzzleKind.OrderedPlates)] [TestCase(ProceduralPuzzleKind.TimedVaults)]
        public void DeadlineIsExclusiveAndWaitingDoesNotSolve(ProceduralPuzzleKind kind)
        {
            var s = new ProceduralPuzzleDriverState(); Step(s, kind, 0, vault: true);
            Step(s, kind, 1, 6f, vault: true);
            Assert.That(s.Next, Is.Zero); Assert.That(s.GateOpen, Is.False);
            Step(s, kind, 0, vault: true);
            Assert.That(s.Next, Is.EqualTo(1), "Expiry must not retain the first-contact debounce.");
        }
        [TestCase(-1)] [TestCase(0)]
        public void LitPathResetsOnDepartureOrExtinguishedTile(int tile)
        {
            var s = new ProceduralPuzzleDriverState(); Step(s, ProceduralPuzzleKind.DimmingPath, 0);
            Step(s, ProceduralPuzzleKind.DimmingPath, tile, 1.5f, false);
            Assert.That(s.Started, Is.False); Assert.That(s.Next, Is.Zero);
        }
        [Test]
        public void OnlyCompletedVaultsAdvanceVaultSequence()
        {
            var s = new ProceduralPuzzleDriverState(); Step(s, ProceduralPuzzleKind.TimedVaults, 0);
            Assert.That(s.Next, Is.Zero);
            Step(s, ProceduralPuzzleKind.TimedVaults, 0, vault: true);
            _rules.Fail(s); Assert.That(s.Next, Is.Zero);
            Step(s, ProceduralPuzzleKind.TimedVaults, 0, vault: true);
            Assert.That(s.Next, Is.EqualTo(1), "A failed vault must permit a fresh first vault.");
        }
        [Test]
        public void MovingDoorClosesOnStopOrDepartureButDoesNotDuplicateSolution()
        {
            var s = new ProceduralPuzzleDriverState();
            Step(s, ProceduralPuzzleKind.MovingDoor, 2); Assert.That(s.GateOpen, Is.True);
            Step(s, ProceduralPuzzleKind.MovingDoor, 2, speed: 2f); Assert.That(s.GateOpen, Is.False);
            Step(s, ProceduralPuzzleKind.MovingDoor, 3, near: false); Assert.That(s.Solved, Is.False);
            Assert.That(Step(s, ProceduralPuzzleKind.MovingDoor, 3), Is.True);
            Step(s, ProceduralPuzzleKind.MovingDoor, 3, speed: 0f); Assert.That(s.GateOpen, Is.False);
            Assert.That(Step(s, ProceduralPuzzleKind.MovingDoor, 3), Is.False);
        }
        [Test]
        public void DuplicateContactDoesNotAdvanceAndInjectedTimeRepeats()
        {
            var a = new ProceduralPuzzleDriverState(); var b = new ProceduralPuzzleDriverState();
            foreach (var s in new[] { a, b })
            { Step(s, ProceduralPuzzleKind.DimmingPath, 0); Step(s, ProceduralPuzzleKind.DimmingPath, 0, 0.5f); }
            Assert.That(a.Next, Is.EqualTo(1)); Assert.That(a.Elapsed, Is.EqualTo(b.Elapsed));
            Assert.That(_rules.Brightness(a, 0, 1.5f), Is.LessThan(_rules.Brightness(a, 1, 1.5f)));
            Assert.Throws<ArgumentException>(() => Step(a, ProceduralPuzzleKind.OrderedPlates, 0, float.NaN));
        }
    }
}
