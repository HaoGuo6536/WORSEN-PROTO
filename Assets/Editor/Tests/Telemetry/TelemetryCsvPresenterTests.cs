// ============================================================================
// TelemetryCsvPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Checks that exported measurements survive locale changes and CSV quoting. Missing values remain blank while provenance and denominator fields are retained in the same file.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Telemetry.
// KEY RESPONSIBILITIES:
//   - Verify invariant numbers, text escaping and missing-value output.
//   - Lock legacy column order and blank appended cells for every pre-existing kind.
// DEPENDENCIES:
//   - NUnit, Core values and TelemetryCsvPresenter.
// USAGE NOTES:
//   - Editor-only pure formatting tests, no file system required.
// ============================================================================
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Worsen.Core;
using Worsen.Presentation.Telemetry;

namespace Worsen.Tests.Telemetry
{
    public sealed class TelemetryCsvPresenterTests
    {
        [Test]
        public void EveryLegacyKindKeepsItsFirstTenCellsAndHasOnlyBlankAppendedCells()
        {
            var csv = new TelemetryCsvPresenter();
            Assert.That(string.Join(",", csv.Header.Split(',').Take(10)), Is.EqualTo(
                "row_type,tick,player,chase_id,kind,value,detail,in_chase,outcome,event_id"));
            foreach (TelemetrySampleKind kind in System.Enum.GetValues(typeof(TelemetrySampleKind)))
            {
                if (kind >= TelemetrySampleKind.RoundStarted) continue;
                string prefix = "\"raw\",\"7\",\"3\",\"2\",\"" + kind + "\",\"8.125\",\"unchanged\",\"True\",\"Lost\",\"19\"";
                string row = csv.Raw(new TelemetrySample(7, new EntityId(3), 2, kind, 8.125f, "unchanged", true, ChaseEndReason.Lost, 19));
                Assert.That(row, Is.EqualTo(prefix + string.Concat(Enumerable.Repeat(",\"\"", csv.Header.Split(',').Length - 10))), kind.ToString());
            }
            foreach (string row in csv.Metadata(new RunCaptureMetadata("fixture", 1, 0.1f, "s", "c", "order", 0))
                .Concat(csv.Summary(new TelemetryReport(), 10)))
                Assert.That(row, Does.EndWith(string.Concat(Enumerable.Repeat(",\"\"", csv.Header.Split(',').Length - 10))));
        }
        [Test]
        public void RawRowsUseInvariantNumbersAndEscapeQuotesCommasAndNewlines()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                string row = new TelemetryCsvPresenter().Raw(new TelemetrySample(1, new EntityId(1), 0,
                    TelemetrySampleKind.HorizontalSpeed, 8.125f, "hello,\"world\"\nnext"));
                Assert.That(row, Does.Contain("\"8.125\""));
                Assert.That(row, Does.Contain("\"hello,\"\"world\"\"\nnext\""));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }
        [Test]
        public void SummaryRetainsDenominatorsAndBlankUnavailableMetrics()
        {
            var csv = new TelemetryCsvPresenter();
            var rows = csv.Summary(new TelemetryReport { CompletedChases = 0, MissingStreams = "chase" }, 10).ToArray();
            Assert.That(rows.Any(row => row.Contains("\"CompletedChases\",\"0\"")), Is.True);
            Assert.That(rows.Any(row => row.Contains("\"LossRate\",\"\"")), Is.True);
            Assert.That(rows.Any(row => row.Contains("\"MissingStreams\",\"chase\"")), Is.True);
        }
    }
}