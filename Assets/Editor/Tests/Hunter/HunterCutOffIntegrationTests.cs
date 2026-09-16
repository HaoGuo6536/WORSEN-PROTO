// ============================================================================
// HunterCutOffIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Follows the actual lower/upper TagArena braid for two circuits and observes
//   whether native room history selects CutOff. The shared harness records real
//   sensors, replans, path targets and motion without seeding any planning fact.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Hunter physical integration.
// KEY RESPONSIBILITIES:
//   - Require a genuine third Hunter room visit, visible replan and intercept target.
//   - Require actual path availability and movement toward the selected room.
// DEPENDENCIES:
//   HunterRouteComparisonTests' shared harness; Domain Hunter; NUnit and Unity tests.
// USAGE NOTES:
//   Coordinator owns Unity admission. Runtime initial poses only, default profiles,
//   no direct Controller/Driver ticks, synthetic sightings, state writes or hints.
//   The existing detector counts Hunter room history; same-room pillar laps do not
//   trigger it. Stop at two physical circuits plus one sensor interval, or a finite
//   config-derived deadline. The original capture closes incomplete at cutoff.
//   Full tick evidence survives negative outcomes; no 30-chase or human claim.
// ============================================================================
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Domain.Hunter;

namespace Worsen.Tests.Hunter
{
    public sealed class HunterCutOffIntegrationTests
    {
        [UnityTest]
        public IEnumerator TwoAuthoredBraidCircuitsProduceNativeRoomHistoryCutOffAndReplan()
        {
            yield return new EnterPlayMode();
            yield return ExerciseBraid();
        }

        private static IEnumerator ExerciseBraid()
        {
            var trial = new AuthoredHunterTrial(AuthoredHunterRoute.BraidCutOff);
            yield return AuthoredHunterTrial.Exercise(trial);
            trial.AssertCommon();
            var report = trial.Report;
            Assert.That(report.actualCircuits, Is.EqualTo(2), trial.ReportPath);
            Assert.That(report.routeFinishedTick, Is.GreaterThan(0));
            Assert.That(report.cutoffTick, Is.EqualTo(report.routeFinishedTick + 4));
            Assert.That(report.rows.Count, Is.EqualTo(report.cutoffTick));
            var cutoffs = report.rows.Where(row => row.cutOffObserved).ToArray();
            Assert.That(cutoffs, Is.Not.Empty, "CutOff must occur through real room/sight facts within the fixed route. " + trial.ReportPath);
            var transitions = cutoffs.Where(row => row.tick > 1 &&
                report.rows[(int)row.tick - 2].action != HunterAction.CutOff.ToString()).ToArray();
            Assert.That(transitions, Is.Not.Empty, "No observed transition into CutOff.");
            Assert.That(transitions.Any(row => row.replans > report.rows[(int)row.tick - 2].replans &&
                row.visible && row.loopDetected && row.observedRoomVisits >= 3 &&
                row.roomHistory.Count(room => room == row.lastRoom) >= 3 && row.roomHistory.Distinct().Count() >= 2), Is.True,
                "The action needs actual repeated Hunter room entries and a visible replan, not a preset flag.");
            Assert.That(cutoffs.Any(row => row.pathAvailable &&
                Vector3.Distance(row.navigationTarget, row.playerPosition) > 0.25f &&
                Vector3.Distance(row.navigationTarget, row.expectedIntercept) < 0.0001f &&
                Vector3.Dot(row.hunterPosition - row.hunterBefore, row.navigationTarget - row.hunterBefore) > 0f), Is.True,
                "A selected action without real motion toward its distinct reachable room target is insufficient.");
            TestContext.WriteLine("Native CutOff rows=" + cutoffs.Length + "; observed room sequence=" +
                string.Join(",", report.roomEntries.Select(entry => entry.room + "@" + entry.tick)) + "; diagnostic=" + trial.ReportPath);
        }

        [UnityTearDown]
        public IEnumerator RestoreEditor() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
