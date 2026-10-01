// ============================================================================
// TelemetryObservationPresenterTests.cs
// ============================================================================
//
// PURPOSE:
//   Checks new progression and navigation rows independently of scene timing.
//   The fixture translates a real Hunter-local stall fact at the Orchestrator
//   boundary and verifies the resulting named CSV cells, including the floor seed.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Telemetry.
//
// KEY RESPONSIBILITIES:
//   - Assert exact rows, identities and stable output labels for every typed operation.
//   - Verify the immutable stall snapshot survives translation without Domain leakage.
//   - Check locale independence and unavailable evidence without engine operations.
//
// DEPENDENCIES:
//   - NUnit, Core values, HunterStallFact, TelemetryOrchestrator and Telemetry presenters.
//
// USAGE NOTES:
//   Pure EditMode tests; no Unity lifecycle, file output, gameplay tick or navigation bake.
//   Synthetic before/after snapshots represent committed actions, not rule decisions.
//
// ============================================================================
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Orchestrator;
using Worsen.Presentation.Telemetry;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Telemetry
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class TelemetryObservationPresenterTests
    {
        private readonly TelemetryObservationPresenter presenter = new TelemetryObservationPresenter();

        [Test]
        public void GenerationProducesExactlyOneRoundStartAndOneExactSeedRow()
        {
            var rows = presenter.Generation(new ProgressionGenerationRequest(18, 2147483647, 4, false, default), 0).ToArray();
            Assert.That(rows.Length, Is.EqualTo(2));
            Assert.That(rows.Count(s => s.Kind == TelemetrySampleKind.RoundStarted), Is.EqualTo(1));
            Assert.That(rows.Count(s => s.Kind == TelemetrySampleKind.FloorSeed), Is.EqualTo(1));
            foreach (TelemetrySample row in rows)
            {
                var cells = Cells(row);
                Assert.That(cells["round_index"], Is.EqualTo("4"));
                Assert.That(cells["generation_id"], Is.EqualTo("18"));
                Assert.That(cells["generation_seed"], Is.EqualTo("2147483647"));
            }
            Assert.That(Cells(rows[0])["round_outcome"], Is.EqualTo("Started"));
        }

        [TestCase(ProgressionOperation.ChooseThreat, "Threat")]
        [TestCase(ProgressionOperation.ChooseCurse, "Curse")]
        [TestCase(ProgressionOperation.Purchase, "Purchase")]
        public void EachAcceptedChoiceProducesOneRowAndPreservesItsExactId(ProgressionOperation operation, string kind)
        {
            const string id = "id,\"quoted\";=%\nnext";
            var rows = presenter.Transaction(Snapshot(ProgressionPhase.Shop, 4, 12), Snapshot(ProgressionPhase.Shop, 4, 12),
                operation, id, 81, null).ToArray();
            Assert.That(rows.Length, Is.EqualTo(1));
            Assert.That(rows[0].Kind, Is.EqualTo(TelemetrySampleKind.ProgressionChoice));
            var cells = Cells(rows[0]);
            Assert.That(cells["choice_id"], Is.EqualTo(id));
            Assert.That(cells["choice_kind"], Is.EqualTo(kind));
            Assert.That(cells["tick"], Is.EqualTo("81"));
        }

        [TestCase(ProgressionOperation.RecordGoldenCollected, "RecordGoldenCollected", 12, 14, "2")]
        [TestCase(ProgressionOperation.Purchase, "Purchase", 14, 3, "-11")]
        [TestCase(ProgressionOperation.EndRun, "EndRun", 6, 0, "-6")]
        [TestCase(ProgressionOperation.StartRun, "StartRun", 5, 0, "-5")]
        public void EveryBalanceChangeProducesOneSignedWalletRow(ProgressionOperation operation, string reason, int before, int after, string delta)
        {
            var rows = presenter.Transaction(Snapshot(ProgressionPhase.Shop, 4, before), Snapshot(ProgressionPhase.Shop, 4, after),
                operation, "purchase", 99, 1937123456).Where(s => s.Kind == TelemetrySampleKind.WalletChanged).ToArray();
            Assert.That(rows.Length, Is.EqualTo(1));
            var cells = Cells(rows[0]);
            Assert.That(cells["wallet_delta"], Is.EqualTo(delta));
            Assert.That(cells["wallet_balance"], Is.EqualTo(after.ToString(CultureInfo.InvariantCulture)));
            Assert.That(cells["wallet_reason"], Is.EqualTo(reason));
            Assert.That(cells["generation_seed"], Is.EqualTo("1937123456"));
        }

        [TestCase(ProgressionPhase.Exploring, ProgressionPhase.ChooseThreat, ProgressionOperation.CompleteFloor, "Escaped")]
        [TestCase(ProgressionPhase.Exploring, ProgressionPhase.Ended, ProgressionOperation.RecordHealth, "Died")]
        [TestCase(ProgressionPhase.Shop, ProgressionPhase.ChooseThreat, ProgressionOperation.ContinueShop, "ShopContinued")]
        [TestCase(ProgressionPhase.Generating, ProgressionPhase.GenerationFailed, ProgressionOperation.FailGeneration, "GenerationFailed")]
        public void RoundEndUsesTheCompletedRoundIdentity(ProgressionPhase from, ProgressionPhase to, ProgressionOperation operation, string outcome)
        {
            bool advances = to == ProgressionPhase.ChooseThreat;
            var rows = presenter.Transaction(Snapshot(from, 4, 12), Snapshot(to, advances ? 5 : 4, 12),
                operation, "", 90, 12345).ToArray();
            Assert.That(rows.Length, Is.EqualTo(1));
            Assert.That(rows[0].Kind, Is.EqualTo(TelemetrySampleKind.RoundEnded));
            var cells = Cells(rows[0]);
            Assert.That(cells["round_index"], Is.EqualTo("4"));
            Assert.That(cells["generation_id"], Is.EqualTo("18"));
            Assert.That(cells["round_outcome"], Is.EqualTo(outcome));
        }

        [Test]
        public void NonObservationalChangesDoNotInventRows()
        {
            Assert.That(presenter.Transaction(Snapshot(ProgressionPhase.Exploring, 4, 12), Snapshot(ProgressionPhase.Exploring, 4, 12),
                ProgressionOperation.RecordHealth, "", 6, null), Is.Empty);
            Assert.That(presenter.Transaction(Snapshot(ProgressionPhase.Generating, 4, 12), Snapshot(ProgressionPhase.Exploring, 4, 12),
                ProgressionOperation.ConfirmFloorReady, "", 0, 123), Is.Empty, "Readiness must not duplicate the generation's round start.");
        }

        [TestCase(ProgressionOperation.StartRun, "StartRun")]
        [TestCase(ProgressionOperation.ChooseThreat, "ChooseThreat")]
        [TestCase(ProgressionOperation.ChooseCurse, "ChooseCurse")]
        [TestCase(ProgressionOperation.TakeBargain, "TakeBargain")]
        [TestCase(ProgressionOperation.Purchase, "Purchase")]
        [TestCase(ProgressionOperation.ReservePurchase, "ReservePurchase")]
        [TestCase(ProgressionOperation.RerollShop, "RerollShop")]
        [TestCase(ProgressionOperation.RerollSelection, "RerollSelection")]
        [TestCase(ProgressionOperation.CancelReplacement, "CancelReplacement")]
        [TestCase(ProgressionOperation.ContinueShop, "ContinueShop")]
        [TestCase(ProgressionOperation.ConfirmFloorReady, "ConfirmFloorReady")]
        [TestCase(ProgressionOperation.FailGeneration, "FailGeneration")]
        [TestCase(ProgressionOperation.CompleteFloor, "CompleteFloor")]
        [TestCase(ProgressionOperation.RecordGoldenCollected, "RecordGoldenCollected")]
        [TestCase(ProgressionOperation.TryConsumeWaxWard, "TryConsumeWaxWard")]
        [TestCase(ProgressionOperation.CycleConsumable, "CycleConsumable")]
        [TestCase(ProgressionOperation.TryConsumeSelected, "TryConsumeSelected")]
        [TestCase(ProgressionOperation.TryConsumeExtraLife, "TryConsumeExtraLife")]
        [TestCase(ProgressionOperation.RecordHealth, "RecordHealth")]
        [TestCase(ProgressionOperation.EndRun, "EndRun")]
        [TestCase(ProgressionOperation.ActivateShrine, "ActivateShrine")]
        public void EveryOperationKeepsLegacyWalletLabelAndChoiceRouting(ProgressionOperation operation, string label)
        {
            var rows = presenter.Transaction(Snapshot(ProgressionPhase.Shop, 4, 12), Snapshot(ProgressionPhase.Shop, 4, 10),
                operation, "exact-id", 81, 12345).ToArray();
            Assert.That(Cells(rows[0])["wallet_reason"], Is.EqualTo(label));
            string kind = operation == ProgressionOperation.ChooseThreat ? "Threat" :
                operation == ProgressionOperation.ChooseCurse ? "Curse" : operation == ProgressionOperation.Purchase ? "Purchase" : null;
            Assert.That(rows.Length, Is.EqualTo(kind == null ? 1 : 2));
            Assert.That(rows[0].Kind, Is.EqualTo(TelemetrySampleKind.WalletChanged));
            if (kind == null) return;
            Assert.That(rows[1].Kind, Is.EqualTo(TelemetrySampleKind.ProgressionChoice));
            Assert.That(Cells(rows[1])["choice_kind"], Is.EqualTo(kind));
            Assert.That(Cells(rows[1])["choice_id"], Is.EqualTo("exact-id"));
        }

        [Test]
        public void UnknownOperationCannotWriteAnInventedWalletLabel()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => presenter.Transaction(
                Snapshot(ProgressionPhase.Shop, 4, 12), Snapshot(ProgressionPhase.Shop, 4, 10),
                (ProgressionOperation)int.MaxValue, "", 81, null).ToArray());
        }

        [Test]
        public void HunterLocalStallBecomesOneTelemetrySampleWithSeedAndAllEvidence()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var corners = new[] { new Vector3(1.25f, 2, 3), new Vector3(4, 5, 6) };
                var fact = new HunterStallFact(new EntityId(21), 78, new Vector3(-1.5f, 2.25f, 3.5f), 9,
                    corners, 0.2f, 0.35f, 0.4f, HunterAction.Chase, 7.125, new Vector3(8, 9, 10));
                corners[0] = Vector3.zero;
                TelemetrySample sample = TelemetryOrchestrator.TranslateStall(fact, "rusher", 2147483647);
                Assert.That(sample.Kind, Is.EqualTo(TelemetrySampleKind.HunterStall));
                var cells = Cells(sample);
                var expected = new Dictionary<string, string>
                {
                    ["tick"] = "78", ["hunter_id"] = "21", ["archetype_key"] = "rusher", ["generation_seed"] = "2147483647",
                    ["position_x"] = "-1.5", ["position_y"] = "2.25", ["position_z"] = "3.5", ["room_id"] = "9",
                    ["remaining_distance"] = "7.125", ["query_radius"] = "0.2", ["capsule_radius"] = "0.35",
                    ["sweep_radius"] = "0.4", ["action"] = "Chase", ["corner_count"] = "2", ["path_corners"] = "1.25:2:3|4:5:6",
                    ["nearest_obstacle_x"] = "8", ["nearest_obstacle_y"] = "9", ["nearest_obstacle_z"] = "10"
                };
                foreach (var field in expected) Assert.That(cells[field.Key], Is.EqualTo(field.Value), field.Key);
                Assert.That(new TelemetryCsvPresenter().Raw(sample).Count(c => c == '\n'), Is.Zero);
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test]
        public void MissingStallRoomAndObstacleRemainBlank()
        {
            var fact = new HunterStallFact(new EntityId(21), 78, Vector3.zero, null, new Vector3[0],
                0.2f, 0.35f, 0.4f, HunterAction.Patrol, 2);
            var cells = Cells(TelemetryOrchestrator.TranslateStall(fact, "watcher", -123));
            Assert.That(cells["room_id"], Is.Empty);
            Assert.That(cells["nearest_obstacle_x"], Is.Empty);
            Assert.That(cells["corner_count"], Is.EqualTo("0"));
            Assert.That(cells["generation_seed"], Is.EqualTo("-123"));
        }

        private static ProgressionSnapshot Snapshot(ProgressionPhase phase, int round, int wallet) =>
            new ProgressionSnapshot(1, 18, round, 777, wallet, 1, 1, phase, 100, 100,
                null, null, null, default, "", false, false);

        private static Dictionary<string, string> Cells(TelemetrySample sample)
        {
            var csv = new TelemetryCsvPresenter();
            string[] names = csv.Header.Split(',');
            var values = Regex.Matches(csv.Raw(sample), "\"((?:[^\"]|\"\")*)\"(?:,|$)")
                .Cast<Match>().Select(m => m.Groups[1].Value.Replace("\"\"", "\"")).ToArray();
            Assert.That(values.Length, Is.EqualTo(names.Length), "Exactly one full-width CSV row is required.");
            return names.Select((name, index) => new { name, value = values[index] }).ToDictionary(p => p.name, p => p.value);
        }
    }
}
