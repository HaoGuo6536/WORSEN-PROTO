// ============================================================================
// ResultsPresenterTests.cs
// ============================================================================
//
// PURPOSE:
//   Verifies authoritative summary formatting and repeat-click behavior without a UI document.
//   These tests distinguish unavailable values from zero and ensure hiding the display
//   ends the current restart cycle before a fresh summary can rearm it.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Results.
//
// KEY RESPONSIBILITIES:
//   - Verify all visible summary facts, culture-independent duration text and outcomes.
//   - Verify a single accepted restart, duplicate-summary debounce and hide/reset behavior.
//
// DEPENDENCIES:
//   - Worsen.Core summaries, Worsen.Presentation.Results and NUnit.
//
// USAGE NOTES:
//   - Editor-only pure tests. No scene loading, engine objects or frame-time access.
//
// ============================================================================

using System.Globalization;
using NUnit.Framework;
using Worsen.Core;
using Worsen.Presentation.Results;

namespace Worsen.Tests.Results
{
    public sealed class ResultsPresenterTests
    {
        [Test]
        public void SummaryPreservesAllAuthoritativeCountsAndTimes()
        {
            var state = new ResultsDriverState();
            new ResultsPresenter().Show(state, new RunSummary(185.9, 8, 2, 4, 3, 75.8, RunEndReason.Escaped));
            Assert.That(state.Visible, Is.True);
            Assert.That(state.RunTime, Is.EqualTo("03:05"));
            Assert.That(state.Cakes, Is.EqualTo("8"));
            Assert.That(state.GoldenCakes, Is.EqualTo("2"));
            Assert.That(state.Chases, Is.EqualTo("4"));
            Assert.That(state.Escapes, Is.EqualTo("3"));
            Assert.That(state.ChaseTime, Is.EqualTo("01:15"));
            Assert.That(state.EndReason, Is.EqualTo("Escaped through the exit"));
        }

        [Test]
        public void RestartAcceptsOneVisibleClickAndRepeatedSummaryDoesNotRearm()
        {
            var state = new ResultsDriverState();
            var presenter = new ResultsPresenter();
            var summary = new RunSummary(10, 1, 0, 1, 0, 5, RunEndReason.Died);
            Assert.That(presenter.TryRestart(state), Is.False);
            presenter.Show(state, summary);
            Assert.That(presenter.TryRestart(state), Is.True);
            Assert.That(presenter.TryRestart(state), Is.False);
            presenter.Show(state, summary);
            Assert.That(presenter.TryRestart(state), Is.False);
            Assert.That(state.EndReason, Is.EqualTo("You died"));
        }

        [Test]
        public void HideRejectsStaleClicksAndFreshDisplayRearms()
        {
            var state = new ResultsDriverState();
            var presenter = new ResultsPresenter();
            var summary = new RunSummary(0, 0, 0, 0, 0, 0, RunEndReason.Unknown);
            presenter.Show(state, summary);
            Assert.That(presenter.TryRestart(state), Is.True);
            presenter.Hide(state);
            Assert.That(state.Visible, Is.False);
            Assert.That(state.RunTime, Is.EqualTo("—"));
            Assert.That(presenter.TryRestart(state), Is.False);
            presenter.Show(state, summary);
            Assert.That(presenter.TryRestart(state), Is.True);
            Assert.That(state.EndReason, Is.EqualTo("Unknown outcome"));
        }

        [TestCase(0d, "00:00")]
        [TestCase(59.999d, "00:59")]
        [TestCase(60d, "01:00")]
        [TestCase(3601d, "60:01")]
        [TestCase(-1d, "—")]
        [TestCase(double.NaN, "—")]
        [TestCase(double.PositiveInfinity, "—")]
        [TestCase(double.MaxValue, "—")]
        public void DurationFormattingHandlesBoundariesAndUnavailableValues(double seconds, string expected)
        {
            Assert.That(new ResultsPresenter().FormatDuration(seconds), Is.EqualTo(expected));
        }

        [Test]
        public void InvalidSamplesRemainUnavailableAndFormattingIgnoresCurrentCulture()
        {
            var oldCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                var state = new ResultsDriverState();
                var presenter = new ResultsPresenter();
                presenter.SetSummary(state, 62d, 1234, -1, 0, -1, double.NaN, "");
                Assert.That(state.RunTime, Is.EqualTo("01:02"));
                Assert.That(state.Cakes, Is.EqualTo("1234"));
                Assert.That(state.GoldenCakes, Is.EqualTo("—"));
                Assert.That(state.Chases, Is.EqualTo("0"));
                Assert.That(state.Escapes, Is.EqualTo("—"));
                Assert.That(state.ChaseTime, Is.EqualTo("—"));
                Assert.That(state.EndReason, Is.EqualTo("Unknown outcome"));
            }
            finally { CultureInfo.CurrentCulture = oldCulture; }
        }
    }
}

