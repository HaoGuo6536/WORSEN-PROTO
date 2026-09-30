// ============================================================================
// GenerationFailureObservationTests.cs
// ============================================================================
// PURPOSE:
//   Checks fallback evidence travels in the existing failure row, not a success row.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Telemetry.
// KEY RESPONSIBILITIES:
//   - Verify exact manifest hash, blank unavailable evidence and preserved CSV Detail.
// DEPENDENCIES:
//   NUnit, Core snapshots and pure Telemetry presenters.
// USAGE NOTES:
//   Edit Mode; no files, runtime capture, network or scene objects.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using Worsen.Core;
using Worsen.Presentation.Telemetry;

namespace Worsen.Tests.Telemetry
{
    public sealed class GenerationFailureObservationTests
    {
        [TestCase(true, "True")] [TestCase(false, "False")]
        public void FailureHasOneRoundRowWithFallbackAndUtf8ManifestHash(bool fallback, string value)
        {
            var rows = new TelemetryObservationPresenter().Transaction(Snapshot(ProgressionPhase.Generating),
                Snapshot(ProgressionPhase.GenerationFailed), "FailGeneration", "", 9, -731, fallback, "abc").ToArray();
            Assert.That(rows.Length, Is.EqualTo(1)); Assert.That(rows[0].Kind, Is.EqualTo(TelemetrySampleKind.RoundEnded));
            const string digest = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
            Assert.That(rows[0].Detail, Does.Contain("round_outcome=GenerationFailed"));
            Assert.That(rows[0].Detail, Does.Contain("used_fallback=" + value));
            Assert.That(rows[0].Detail, Does.Contain("layout_manifest_hash=" + digest));
            Assert.That(new TelemetryCsvPresenter().Raw(rows[0]), Does.Contain(digest));
            Assert.That(new TelemetryObservationPresenter().Transaction(Snapshot(ProgressionPhase.Generating),
                Snapshot(ProgressionPhase.Exploring), "ConfirmFloorReady", "", 9, -731, false, "abc"), Is.Empty);
        }
        [Test] public void MissingEvidenceIsBlankNotFabricated()
        {
            Assert.That(TelemetryObservationPresenter.ManifestHash(null), Is.Empty);
            var row = new TelemetryObservationPresenter().Transaction(Snapshot(ProgressionPhase.Generating),
                Snapshot(ProgressionPhase.GenerationFailed), "FailGeneration", "", 9, null).Single();
            Assert.That(row.Detail, Does.Contain("used_fallback=;layout_manifest_hash="));
        }
        private static ProgressionSnapshot Snapshot(ProgressionPhase phase) => new ProgressionSnapshot(1, 18, 4, 777,
            0, 1, 1, phase, 100, 100, null, null, null, default, "", false, false);
    }
}
