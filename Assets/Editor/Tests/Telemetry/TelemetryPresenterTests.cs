// ============================================================================
// TelemetryPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Tests measurement definitions with controlled evidence, including absent and interrupted streams. It proves that rates use explicit denominators and that a replay fixture cannot be mistaken for measured acceptance.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Telemetry.
// KEY RESPONSIBILITIES:
//   - Exercise boundaries, invalid/duplicate rows, speed filtering, chase denominators, locks and resolved vault outcomes.
// DEPENDENCIES:
//   - NUnit, Core immutable facts and own telemetry presenters.
// USAGE NOTES:
//   - Editor-only pure tests; no live gameplay or human perception claims.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
using Worsen.Presentation.Telemetry;

namespace Worsen.Tests.Telemetry
{
    public sealed class TelemetryPresenterTests
    {
        private TelemetryPresenter _p;
        private TelemetryDriverState _s;
        private static readonly EntityId Player = new EntityId(1);
        [SetUp]
        public void SetUp()
        {
            _p = new TelemetryPresenter(); _s = new TelemetryDriverState();
            _p.Begin(_s, new RunCaptureMetadata("fixture", 7, 0.1f, "source", "config", "Player>Floor>Director", 0));
        }
        [Test]
        public void MissingStreamsProduceNullMetricsInsteadOfPassingZeroes()
        {
            var r = _p.Finish(_s, 100, false);
            Assert.That(r.Complete, Is.False); Assert.That(r.LossRate, Is.Null); Assert.That(r.FreeSpeedP90, Is.Null);
            Assert.That(r.LungeCatchRate, Is.Null); Assert.That(r.MissingStreams, Does.Contain("chase"));
            Assert.That(r.MaximumProximityGapSeconds, Is.Null);
        }
        [Test]
        public void RatesUseKnownChasesAndCaughtChasesWithUnknownAndIncompleteSeparate()
        {
            for (int id = 1; id <= 6; id++) Add(0, TelemetrySampleKind.ChaseStarted, chase: id);
            Add(100, TelemetrySampleKind.ChaseEnded, chase: 1, outcome: ChaseEndReason.Lost);
            Add(200, TelemetrySampleKind.ChaseEnded, chase: 2, outcome: ChaseEndReason.Lost);
            Add(300, TelemetrySampleKind.ChaseEnded, chase: 3, outcome: ChaseEndReason.Lunge);
            Add(400, TelemetrySampleKind.ChaseEnded, chase: 4, outcome: ChaseEndReason.Cornered);
            Add(500, TelemetrySampleKind.ChaseEnded, chase: 5, outcome: ChaseEndReason.Unknown);
            var r = _p.Finish(_s, 600, true);
            Assert.That(r.CompletedChases, Is.EqualTo(4)); Assert.That(r.UnknownChases, Is.EqualTo(1));
            Assert.That(r.IncompleteChases, Is.EqualTo(1)); Assert.That(r.LossRate, Is.EqualTo(0.5));
            Assert.That(r.Catches, Is.EqualTo(2)); Assert.That(r.LungeCatchRate, Is.EqualTo(0.5));
            Assert.That(r.MedianChaseSeconds, Is.EqualTo(25).Within(0.00001));
        }
        [Test]
        public void HorizontalFreePercentileExcludesChaseAndInvalidValues()
        {
            for (int i = 1; i <= 10; i++) Add(i, TelemetrySampleKind.HorizontalSpeed, i);
            Add(11, TelemetrySampleKind.HorizontalSpeed, 999, inChase: true);
            Add(12, TelemetrySampleKind.HorizontalSpeed, float.NaN);
            Add(13, TelemetrySampleKind.HorizontalSpeed, -1);
            var r = _p.Finish(_s, 20, true);
            Assert.That(r.FreeSpeedSamples, Is.EqualTo(10)); Assert.That(r.FreeSpeedP90, Is.EqualTo(9));
            Assert.That(r.ChaseSpeedSamples, Is.EqualTo(1)); Assert.That(r.InvalidSamples, Is.EqualTo(2));
        }
        [Test]
        public void ConversionExcludesVerticalMotionAndEmitsOneEdgePerChange()
        {
            var first = Movement(1, true, 0.2f, MovementState.Vault);
            foreach (var row in _p.ConvertMovement(_s, first)) _p.Record(_s, row);
            foreach (var row in _p.ConvertMovement(_s, Movement(2, true, 0.1f, MovementState.Vault))) _p.Record(_s, row);
            foreach (var row in _p.ConvertMovement(_s, Movement(3, false, 0, MovementState.Ground))) _p.Record(_s, row);
            var r = _p.Finish(_s, 4, false);
            Assert.That(r.FreeSpeedP90, Is.EqualTo(5)); Assert.That(r.LookBacks, Is.EqualTo(1));
            Assert.That(r.CompletedLocks, Is.EqualTo(1)); Assert.That(r.MaximumInputLockSeconds, Is.EqualTo(0.2).Within(0.000001));
            Assert.That(r.IncompleteVaultAttempts, Is.EqualTo(1)); Assert.That(r.CatchWindowsUnknown, Is.EqualTo(1));
        }
        [Test]
        public void EventIdsDeduplicateWhileOutOfOrderRowsAreSorted()
        {
            Add(20, TelemetrySampleKind.ChaseEnded, chase: 1, outcome: ChaseEndReason.Lost, eventId: 22);
            Add(0, TelemetrySampleKind.ChaseStarted, chase: 1, eventId: 11);
            Add(20, TelemetrySampleKind.ChaseEnded, chase: 1, outcome: ChaseEndReason.Lost, eventId: 22);
            var r = _p.Finish(_s, 30, true);
            Assert.That(r.RawSamples, Is.EqualTo(3)); Assert.That(r.DuplicateSamples, Is.EqualTo(1));
            Assert.That(r.OutOfOrderSamples, Is.EqualTo(1)); Assert.That(r.CompletedChases, Is.EqualTo(1));
        }
        [TestCase(30, true)]
        [TestCase(31, false)]
        public void CatchWithinOneSecondUsesInclusiveReleaseBoundary(int catchTick, bool caught)
        {
            Add(0, TelemetrySampleKind.ChaseStarted, chase: 1);
            Add(10, TelemetrySampleKind.LookBackStarted, chase: 1, inChase: true);
            Add(20, TelemetrySampleKind.LookBackEnded, chase: 1, inChase: true);
            Add(catchTick, TelemetrySampleKind.ChaseEnded, chase: 1, outcome: ChaseEndReason.Lunge);
            var r = _p.Finish(_s, 50, true);
            Assert.That(r.CatchWindowsKnown, Is.EqualTo(1)); Assert.That(r.CatchesWithinWindow, Is.EqualTo(caught ? 1 : 0));
            Assert.That(r.LookBacksPerChase, Is.EqualTo(1));
        }
        [Test]
        public void ShortCaptureAndUnmatchedIntervalsRemainIncomplete()
        {
            Add(0, TelemetrySampleKind.ChaseStarted, chase: 1);
            Add(1, TelemetrySampleKind.LookBackStarted);
            Add(2, TelemetrySampleKind.LookBackEnded);
            Add(3, TelemetrySampleKind.InputLockStarted, detail: "Vault");
            Add(4, TelemetrySampleKind.LookBackStarted);
            var r = _p.Finish(_s, 5, false);
            Assert.That(r.CatchWindowsUnknown, Is.EqualTo(1)); Assert.That(r.CatchWithinOneSecondRate, Is.Null);
            Assert.That(r.IncompleteLocks, Is.EqualTo(1)); Assert.That(r.IncompleteLookBacks, Is.EqualTo(1));
        }
        [Test]
        public void ResolvedVaultOutcomeCountsOnceAndOrphanFailureCannotBiasRate()
        {
            foreach (var row in _p.ConvertTraversal(_s, new PlayerTraversalFact(Player, 1, TraversalKind.Vault, false, default, 0.2f))) _p.Record(_s, row);
            foreach (var row in _p.ConvertTraversal(_s, new PlayerTraversalFact(Player, 2, TraversalKind.Mantle, true, default, 0.3f))) _p.Record(_s, row);
            Add(3, TelemetrySampleKind.VaultFailed, inChase: true);
            var r = _p.Finish(_s, 5, true);
            Assert.That(r.FreeVaultAttempts, Is.EqualTo(2)); Assert.That(r.FreeVaultFailures, Is.EqualTo(1));
            Assert.That(r.FreeVaultFailureRate, Is.EqualTo(0.5)); Assert.That(r.ChaseVaultFailureRate, Is.Null);
            Assert.That(r.VaultFailureRateIncrease, Is.Null); Assert.That(r.InvalidSamples, Is.EqualTo(1));
        }
        [Test]
        public void CaptureRestartDropsOldRowsAndPriorEdges()
        {
            Add(1, TelemetrySampleKind.HorizontalSpeed, 99);
            _p.Begin(_s, new RunCaptureMetadata("second", 9, 0.1f, "s", "c", "order", 0));
            Assert.That(_s.Samples, Is.Empty); Assert.That(_s.PreviousMovement, Is.Empty);
            Assert.That(_p.Finish(_s, 0, false).FreeSpeedP90, Is.Null);
        }
        private static PlayerMovementSample Movement(long tick, bool look, float inputLock, MovementState movement) =>
            new PlayerMovementSample(Player, tick, default, new Vector3(3, 100, 4), default, 0, default, look, movement, inputLock);
        private void Add(long tick, TelemetrySampleKind kind, float value = 0, int chase = 0,
            bool inChase = false, ChaseEndReason outcome = ChaseEndReason.Unknown, long eventId = 0, string detail = "") =>
            _p.Record(_s, new TelemetrySample(tick, Player, chase, kind, value, detail, inChase, outcome, eventId));
    }
}
