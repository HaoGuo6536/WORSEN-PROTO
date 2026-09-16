// ============================================================================
// TelemetryIntegrationPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies committed chase tags and independent accepted-hit accounting with
//   controlled multi-player captures, including incomplete and legacy evidence.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Telemetry.
// KEY RESPONSIBILITIES:
//   - Exercise inclusive intervals, identity isolation, catch union and CSV denominators.
// DEPENDENCIES:
//   - NUnit, Core values and own pure Telemetry presenters.
// USAGE NOTES:
//   - Pure tests; no live routing, player feel or measured gameplay acceptance claims.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Telemetry;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Telemetry
{
    public sealed class TelemetryIntegrationPresenterTests
    {
        private TelemetryPresenter _presenter;
        private TelemetryDriverState _state;
        private static readonly EntityId Player = new EntityId(1);
        private static readonly EntityId Other = new EntityId(2);

        [SetUp]
        public void SetUp()
        {
            _presenter = new TelemetryPresenter(); _state = new TelemetryDriverState();
            _presenter.Begin(_state, new RunCaptureMetadata("integration-fixture", 1, 0.1f, "source", "config", "order", 0));
        }

        [TestCase(9, false)]
        [TestCase(10, true)]
        [TestCase(20, true)]
        [TestCase(21, false)]
        public void MovementAndTraversalUseInclusiveChaseInterval(int tick, bool expected)
        {
            Add(10, TelemetrySampleKind.ChaseStarted, 7);
            Add(20, TelemetrySampleKind.ChaseEnded, 7, ChaseEndReason.Lost);
            var rows = _presenter.ConvertMovement(_state, Movement(Player, tick, true, 0.2f));
            Assert.That(rows.Length, Is.EqualTo(3));
            Assert.That(rows.All(r => r.InChase == expected && r.ChaseId == (expected ? 7 : 0)), Is.True);
            Assert.That(rows[0].Value, Is.EqualTo(5));
            var traversal = _presenter.ConvertTraversal(_state, new PlayerTraversalFact(Player, tick, TraversalKind.Vault, false, default, 0.2f));
            Assert.That(traversal.Length, Is.EqualTo(2));
            Assert.That(traversal.All(r => r.InChase == expected && r.ChaseId == (expected ? 7 : 0)), Is.True);
        }

        [Test]
        public void SameTickStartAndEndTagsOnlyThatTick()
        {
            Add(10, TelemetrySampleKind.ChaseEnded, 7, ChaseEndReason.Lunge);
            Add(10, TelemetrySampleKind.ChaseStarted, 7);
            Assert.That(_presenter.ConvertMovement(_state, Movement(Player, 10))[0].ChaseId, Is.EqualTo(7));
            Assert.That(_presenter.ConvertMovement(_state, Movement(Player, 11))[0].InChase, Is.False);
            var report = _presenter.Finish(_state, 20, true);
            Assert.That(report.StartedChases, Is.EqualTo(1));
            Assert.That(report.CompletedChases, Is.EqualTo(1));
            Assert.That(report.MedianChaseSeconds, Is.Zero);
        }

        [Test]
        public void AdjacentChasesPreferLatestStartAndPlayersStayIsolated()
        {
            Add(1, TelemetrySampleKind.ChaseStarted, 7);
            Add(20, TelemetrySampleKind.ChaseEnded, 7, ChaseEndReason.Lost);
            Add(20, TelemetrySampleKind.ChaseStarted, 8);
            Add(1, TelemetrySampleKind.ChaseStarted, 9, player: Other);
            Assert.That(_presenter.ConvertMovement(_state, Movement(Player, 20))[0].ChaseId, Is.EqualTo(8));
            Assert.That(_presenter.ConvertMovement(_state, Movement(Other, 20))[0].ChaseId, Is.EqualTo(9));
            Assert.That(_presenter.ConvertMovement(_state, Movement(new EntityId(3), 20))[0].ChaseId, Is.Zero);
        }

        [Test]
        public void MalformedDuplicateAndOutOfOrderTransitionsDoNotPoisonTags()
        {
            Add(20, TelemetrySampleKind.ChaseEnded, 7, ChaseEndReason.Lost, 2);
            Add(5, TelemetrySampleKind.ChaseStarted, 0, eventId: 1);
            Add(10, TelemetrySampleKind.ChaseStarted, 7, eventId: 1);
            Add(20, TelemetrySampleKind.ChaseEnded, 7, ChaseEndReason.Lost, 2);
            Add(12, TelemetrySampleKind.ChaseStarted, 7);
            Add(30, TelemetrySampleKind.ChaseEnded, 99, ChaseEndReason.Lost);
            Assert.That(_presenter.ConvertMovement(_state, Movement(Player, 15))[0].ChaseId, Is.EqualTo(7));
            Assert.That(_presenter.ConvertMovement(_state, Movement(Player, 31))[0].ChaseId, Is.Zero);
            var report = _presenter.Finish(_state, 40, true);
            Assert.That(report.InvalidSamples, Is.EqualTo(3));
            Assert.That(report.DuplicateSamples, Is.EqualTo(1));
            Assert.That(report.CompletedChases, Is.EqualTo(1));
            Assert.That(report.OutOfOrderSamples, Is.GreaterThan(0));
        }

        [Test]
        public void BackdatedTransitionAfterConversionMarksRawTagsIncomplete()
        {
            var emitted = _presenter.ConvertMovement(_state, Movement(Player, 10));
            Add(10, TelemetrySampleKind.ChaseStarted, 7);
            Assert.That(emitted[0].InChase, Is.False);
            Assert.That(_presenter.ConvertMovement(_state, Movement(Player, 11))[0].InChase, Is.True);
            var report = _presenter.Finish(_state, 20, true);
            Assert.That(report.Complete, Is.False);
            Assert.That(report.LateChaseTransitions, Is.EqualTo(1));
        }

        [Test]
        public void SameTickAcceptedHitAndMatchingEndPreserveAlreadyTaggedMovement()
        {
            Add(1, TelemetrySampleKind.ChaseStarted, 7, eventId: 1);
            var emitted = _presenter.ConvertMovement(_state, Movement(Player, 10, true, 0.2f));
            foreach (var row in emitted) _presenter.Record(_state, row);
            Add(10, TelemetrySampleKind.AcceptedHit, 7, ChaseEndReason.Lunge, 2);
            Add(10, TelemetrySampleKind.ChaseEnded, 7, ChaseEndReason.Lunge, 3);
            var report = _presenter.Finish(_state, 10, true);
            Assert.That(emitted.All(row => row.InChase && row.ChaseId == 7), Is.True);
            Assert.That(report.Complete, Is.True);
            Assert.That(report.LateChaseTransitions, Is.Zero);
            Assert.That(report.ChaseSpeedSamples, Is.EqualTo(1));
            Assert.That(report.Catches, Is.EqualTo(1));
            Assert.That(report.CompletedChases, Is.EqualTo(1));
        }

        [TestCase(9, 7)]
        [TestCase(10, 8)]
        public void EarlierOrDifferentChaseEndCannotUseTerminalTagException(int endTick, int chaseId)
        {
            Add(1, TelemetrySampleKind.ChaseStarted, 7);
            Add(1, TelemetrySampleKind.ChaseStarted, 8);
            Assert.That(_presenter.ConvertMovement(_state, Movement(Player, 10))[0].ChaseId, Is.EqualTo(7));
            Add(endTick, TelemetrySampleKind.ChaseEnded, chaseId, ChaseEndReason.Lunge);
            var report = _presenter.Finish(_state, 10, true);
            Assert.That(report.Complete, Is.False);
            Assert.That(report.LateChaseTransitions, Is.EqualTo(1));
        }

        [TestCase(7, true)]
        [TestCase(8, false)]
        public void OlderTraversalDoesNotReplaceLatestTickAndChaseObservation(int endedChase, bool complete)
        {
            Add(1, TelemetrySampleKind.ChaseStarted, 7);
            Add(1, TelemetrySampleKind.ChaseStarted, 8);
            Assert.That(_presenter.ConvertMovement(_state, Movement(Player, 10))[0].ChaseId, Is.EqualTo(7));
            var earlier = _presenter.ConvertTraversal(_state, new PlayerTraversalFact(Player, 0, TraversalKind.Vault, true, default, 0.2f));
            Assert.That(earlier[0].InChase, Is.False);
            Add(10, TelemetrySampleKind.ChaseEnded, endedChase, ChaseEndReason.Lost);
            var report = _presenter.Finish(_state, 10, true);
            Assert.That(report.Complete, Is.EqualTo(complete));
            Assert.That(report.LateChaseTransitions, Is.EqualTo(complete ? 0 : 1));
        }

        [Test]
        public void RestartClearsTimelineIdentityAndLateDiagnostics()
        {
            Add(1, TelemetrySampleKind.ChaseStarted, 7, eventId: 1);
            _presenter.ConvertMovement(_state, Movement(Player, 2));
            Add(1, TelemetrySampleKind.ChaseStarted, 8);
            _presenter.Begin(_state, new RunCaptureMetadata("next", 1, 0.1f, "s", "c", "o", 0));
            Assert.That(_presenter.ConvertMovement(_state, Movement(Player, 0))[0].InChase, Is.False);
            Add(1, TelemetrySampleKind.ChaseStarted, 9, eventId: 1);
            Assert.That(_presenter.ConvertMovement(_state, Movement(Player, 1))[0].ChaseId, Is.EqualTo(9));
            Assert.That(_state.LateChaseTransitions, Is.Zero);
        }

        [TestCase(ChaseEndReason.Lunge)]
        [TestCase(ChaseEndReason.Cornered)]
        [TestCase(ChaseEndReason.Unknown)]
        public void PreConfirmationHitProvesCatchWithoutManufacturingChase(ChaseEndReason reason)
        {
            Add(10, TelemetrySampleKind.AcceptedHit, outcome: reason, eventId: 1);
            var report = _presenter.Finish(_state, 20, true);
            Assert.That(report.Catches, Is.EqualTo(1));
            Assert.That(report.PreConfirmationCatches, Is.EqualTo(1));
            Assert.That(report.AcceptedHitCatches, Is.EqualTo(1));
            Assert.That(report.OverallOutcomeCount, Is.EqualTo(1));
            Assert.That(report.StartedChases, Is.Zero); Assert.That(report.CompletedChases, Is.Zero);
            Assert.That(report.MedianChaseSeconds, Is.Null);
            Assert.That(report.LossRate, Is.Zero);
            Assert.That(report.UnknownCatchClassifications, Is.EqualTo(reason == ChaseEndReason.Unknown ? 1 : 0));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AcceptedAndLegacyUnionHasExplicitOverallDenominators(bool legacy)
        {
            Add(0, TelemetrySampleKind.ChaseStarted, 1);
            Add(0, TelemetrySampleKind.ChaseStarted, 2);
            Add(0, TelemetrySampleKind.ChaseStarted, 3);
            Add(10, TelemetrySampleKind.ChaseEnded, 1, ChaseEndReason.Lost);
            Add(20, TelemetrySampleKind.ChaseEnded, 2, ChaseEndReason.Lost);
            Add(30, TelemetrySampleKind.ChaseEnded, 3, ChaseEndReason.Lunge);
            Add(30, TelemetrySampleKind.AcceptedHit, 3, ChaseEndReason.Lunge, 30);
            Add(40, TelemetrySampleKind.AcceptedHit, outcome: ChaseEndReason.Cornered, eventId: 40);
            Add(50, TelemetrySampleKind.AcceptedHit, outcome: ChaseEndReason.Unknown, eventId: 50);
            if (legacy)
            {
                Add(0, TelemetrySampleKind.ChaseStarted, 4, player: Other);
                Add(40, TelemetrySampleKind.ChaseEnded, 4, ChaseEndReason.Cornered, player: Other);
            }
            var report = _presenter.Finish(_state, 60, true);
            Assert.That(report.Catches, Is.EqualTo(legacy ? 4 : 3));
            Assert.That(report.Losses, Is.EqualTo(2));
            Assert.That(report.LossRate, Is.EqualTo(legacy ? 2.0 / 6 : 2.0 / 5));
            Assert.That(report.LungeCatchRate, Is.EqualTo(legacy ? 1.0 / 4 : 1.0 / 3));
            Assert.That(report.LegacyCatches, Is.EqualTo(legacy ? 1 : 0));
            Assert.That(report.UnknownCatchClassifications, Is.EqualTo(1));
            Assert.That(report.CompletedChases, Is.EqualTo(legacy ? 4 : 3));
            Assert.That(report.MatchedCatchEnds, Is.EqualTo(1));
            Assert.That(report.CatchAccountingMode, Is.EqualTo(legacy ? "accepted-hit-with-legacy-fallback" : "accepted-hit"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HitAndEndOrderDoesNotDuplicateCatchButDistinctHitsSurvive(bool hitFirst)
        {
            Add(0, TelemetrySampleKind.ChaseStarted, 1);
            if (!hitFirst) Add(10, TelemetrySampleKind.ChaseEnded, 1, ChaseEndReason.Lunge, 3);
            Add(10, TelemetrySampleKind.AcceptedHit, 1, ChaseEndReason.Lunge, 1);
            Add(10, TelemetrySampleKind.AcceptedHit, 1, ChaseEndReason.Lunge, 1);
            Add(10, TelemetrySampleKind.AcceptedHit, 1, ChaseEndReason.Cornered, 2);
            if (hitFirst) Add(10, TelemetrySampleKind.ChaseEnded, 1, ChaseEndReason.Lunge, 3);
            var report = _presenter.Finish(_state, 20, true);
            Assert.That(report.Catches, Is.EqualTo(2)); Assert.That(report.LegacyCatches, Is.Zero);
            Assert.That(report.DuplicateSamples, Is.EqualTo(1)); Assert.That(report.MatchedCatchEnds, Is.EqualTo(1));
            Assert.That(report.CompletedChases, Is.EqualTo(1)); Assert.That(report.MedianChaseSeconds, Is.EqualTo(1).Within(0.00001));
            Assert.That(report.CatchOutcomeConflicts, Is.EqualTo(1));
        }

        [Test]
        public void UnknownLegacyEndRemainsUnknownAndInvalidHitCannotClaimEventId()
        {
            Add(0, TelemetrySampleKind.ChaseStarted, 1);
            Add(10, TelemetrySampleKind.ChaseEnded, 1, ChaseEndReason.Unknown);
            Add(11, TelemetrySampleKind.AcceptedHit, outcome: ChaseEndReason.Lost, eventId: 1);
            Add(12, TelemetrySampleKind.AcceptedHit, outcome: ChaseEndReason.Unknown, eventId: 1);
            var report = _presenter.Finish(_state, 20, true);
            Assert.That(report.UnknownChases, Is.EqualTo(1)); Assert.That(report.CompletedChases, Is.Zero);
            Assert.That(report.Catches, Is.EqualTo(1)); Assert.That(report.UnknownCatchClassifications, Is.EqualTo(1));
            Assert.That(report.InvalidSamples, Is.EqualTo(1)); Assert.That(report.DuplicateSamples, Is.Zero);
        }

        [Test]
        public void AcceptedCatchOverridesConflictingLossWithoutDoubleOutcome()
        {
            Add(0, TelemetrySampleKind.ChaseStarted, 1);
            Add(10, TelemetrySampleKind.ChaseEnded, 1, ChaseEndReason.Lost);
            Add(10, TelemetrySampleKind.AcceptedHit, 1, ChaseEndReason.Cornered, 1);
            var report = _presenter.Finish(_state, 20, true);
            Assert.That(report.Losses, Is.Zero); Assert.That(report.Catches, Is.EqualTo(1));
            Assert.That(report.CatchOutcomeConflicts, Is.EqualTo(1)); Assert.That(report.OverallOutcomeCount, Is.EqualTo(1));
        }

        [TestCase(30, false, 1, 0)]
        [TestCase(31, false, 0, 0)]
        [TestCase(30, true, 0, 1)]
        public void AcceptedHitCatchWindowIsInclusiveAndPlayerSpecific(int hitTick, bool otherPlayer, int caught, int unknown)
        {
            Add(10, TelemetrySampleKind.LookBackStarted);
            Add(20, TelemetrySampleKind.LookBackEnded);
            Add(hitTick, TelemetrySampleKind.AcceptedHit, outcome: ChaseEndReason.Unknown, eventId: 1, player: otherPlayer ? Other : Player);
            var report = _presenter.Finish(_state, 50, true);
            Assert.That(report.CatchesWithinWindow, Is.EqualTo(caught));
            Assert.That(report.CatchWindowsUnknown, Is.EqualTo(unknown));
            Assert.That(report.StartedChases, Is.Zero);
        }

        [Test]
        public void TruncatedNegativeCatchWindowStaysUnknownEvenWithEarlierAcceptedHit()
        {
            Add(1, TelemetrySampleKind.AcceptedHit, outcome: ChaseEndReason.Lunge, eventId: 1);
            Add(10, TelemetrySampleKind.LookBackStarted); Add(20, TelemetrySampleKind.LookBackEnded);
            var report = _presenter.Finish(_state, 25, false);
            Assert.That(report.CatchWindowsUnknown, Is.EqualTo(1));
            Assert.That(report.CatchWithinOneSecondRate, Is.Null);
        }

        [Test]
        public void CsvExportsNewQualityFieldsAndExplainsLegacyLimits()
        {
            Add(0, TelemetrySampleKind.ChaseStarted, 1);
            Add(10, TelemetrySampleKind.ChaseEnded, 1, ChaseEndReason.Cornered);
            var report = _presenter.Finish(_state, 20, true);
            var csv = new TelemetryCsvPresenter();
            var rows = csv.Summary(report, 20).ToArray();
            foreach (string field in new[] { "AcceptedHitCatches", "PreConfirmationCatches", "LegacyCatches", "MatchedCatchEnds",
                "UnknownCatchClassifications", "CatchOutcomeConflicts", "OverallOutcomeCount", "LateChaseTransitions", "CatchAccountingMode" })
                Assert.That(rows.Count(row => row.Contains("\"" + field + "\"")), Is.EqualTo(1), field);
            Assert.That(report.CatchAccountingMode, Is.EqualTo("legacy-chase-ended"));
            Assert.That(report.LegacyCatches, Is.EqualTo(1));
            var metadata = string.Join("\n", csv.Metadata(_state.Metadata));
            Assert.That(metadata, Does.Contain("loss denominator=Losses+Catches"));
            Assert.That(metadata, Does.Contain("lunge denominator=all catches"));
            Assert.That(metadata, Does.Contain("Legacy end-only captures cannot observe pre-confirmation catches"));
        }

        private static PlayerMovementSample Movement(EntityId player, long tick, bool look = false, float inputLock = 0) =>
            new PlayerMovementSample(player, tick, default, new Vector3(3, 100, 4), default, 0, default, look, MovementState.Ground, inputLock);

        private void Add(long tick, TelemetrySampleKind kind, int chase = 0, ChaseEndReason outcome = ChaseEndReason.Unknown,
            long eventId = 0, EntityId player = default) => _presenter.Record(_state,
                new TelemetrySample(tick, player.IsValid ? player : Player, chase, kind, outcome: outcome, eventId: eventId));
    }
}
