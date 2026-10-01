// ============================================================================
// HorrorRunResultsPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies complete detailed HorrorRun results and exact next-run seed validation.
//   Missing attribution is kept distinct from a hunter kill and death still waits for the catch.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Results.
// KEY RESPONSIBILITIES:
//   - Cover hunter/hand/escape fields, missing values and seed input bounds.
// DEPENDENCIES:
//   NUnit, Core and Results pure presentation.
// USAGE NOTES:
//   Pure EditMode tests; no scene or persistence service required.
// ============================================================================
using NUnit.Framework;
using Worsen.Core;
using Worsen.Presentation.Results;
namespace Worsen.Tests.Results
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HorrorRunResultsPresenterTests
    {
        [TestCase(RunEndReason.Died, DeathCause.Hunter, "echo", "Hunter", "echo", -1, "—")]
        [TestCase(RunEndReason.Died, DeathCause.Hand, "", "Hand", "Unreported", -1, "—")]
        [TestCase(RunEndReason.Escaped, DeathCause.None, "", "Not applicable", "Not applicable", 37, "00:37")]
        public void EveryDetailedFieldSurvivesCatchAndFormatting(RunEndReason end, DeathCause cause, string killer,
            string expectedCause, string expectedKiller, double exitTime, string expectedTime)
        {
            var state = new ResultsDriverState(); var p = new ResultsPresenter();
            p.SetBestDepth(state, 14);
            p.Show(state, new RunSummary(120, 4, 3, 5, 2, 60, end, -42, SceneKey.HorrorRun, cause, killer, 7, exitTime, 12));
            if (end == RunEndReason.Died) { Assert.That(state.Visible, Is.False); p.EndCatch(state, new EntityId(1)); }
            Assert.That(state.Visible, Is.True); Assert.That(state.Cause, Is.EqualTo(expectedCause));
            Assert.That(state.Killer, Is.EqualTo(expectedKiller)); Assert.That(state.Escapes, Is.EqualTo("2"));
            Assert.That(state.GrabsEscaped, Is.EqualTo("7")); Assert.That(state.ExitToEscape, Is.EqualTo(expectedTime));
            Assert.That(state.Depth, Is.EqualTo("12")); Assert.That(state.Seed, Is.EqualTo("-42"));
            Assert.That(state.BestDepth, Is.EqualTo("14"));
            Assert.That(state.RunTime, Is.EqualTo("02:00")); Assert.That(state.Cakes, Is.EqualTo("4"));
            Assert.That(state.GoldenCakes, Is.EqualTo("3")); Assert.That(state.Chases, Is.EqualTo("5"));
            Assert.That(state.ChaseTime, Is.EqualTo("01:00"));
        }
        [TestCase("", true, false, 0)]
        [TestCase("0", true, true, 0)]
        [TestCase("-2147483648", true, true, int.MinValue)]
        [TestCase("2147483647", true, true, int.MaxValue)]
        [TestCase("2147483648", false, true, 0)]
        [TestCase(" 12", false, true, 0)]
        [TestCase("12 ", false, true, 0)]
        [TestCase("1.2", false, true, 0)]
        [TestCase("+12", false, true, 0)]
        [TestCase("-", false, true, 0)]
        [TestCase("١٢", false, true, 0)]
        public void FixedSeedInputIsExactAndInvalidInputBlocksRestart(string text, bool valid, bool fixedSeed, int seed)
        {
            var state = new ResultsDriverState { Visible = true }; var p = new ResultsPresenter();
            Assert.That(p.SetNextSeed(state, text), Is.EqualTo(valid));
            Assert.That(state.NextSeedText, Is.EqualTo(text)); Assert.That(state.UseFixedSeed, Is.EqualTo(fixedSeed));
            if (valid) Assert.That(state.NextSeed, Is.EqualTo(seed));
            Assert.That(p.TryRestart(state), Is.EqualTo(valid));
            Assert.That(state.SeedError.Length > 0, Is.EqualTo(!valid));
            p.Hide(state); Assert.That(state.SeedValid, Is.True); Assert.That(state.UseFixedSeed, Is.False);
        }
        [Test]
        public void MissingProducerDataIsNotInventedAndHistorySurvivesHide()
        {
            var state = new ResultsDriverState(); var p = new ResultsPresenter(); p.SetBestDepth(state, 8);
            p.Show(state, new RunSummary(0, 0, 0, 0, 0, 0, RunEndReason.Died)); p.EndCatch(state, new EntityId(1));
            Assert.That(state.Cause, Is.EqualTo("Unreported")); Assert.That(state.Killer, Is.EqualTo("Unreported"));
            Assert.That(state.Depth, Is.EqualTo("Unreported")); Assert.That(state.ExitToEscape, Is.EqualTo("—"));
            p.Hide(state); Assert.That(state.BestDepth, Is.EqualTo("8"));
        }
    }
}
