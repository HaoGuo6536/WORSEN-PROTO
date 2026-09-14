// ============================================================================
// DebugOverlayPresenterTests.cs
// ============================================================================
//
// PURPOSE:
//   Verifies development-overlay text with plain data and no scene. These tests
//   protect the distinction between absent player telemetry and a real stopped
//   player, and keep numeric output stable when the machine's locale changes.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · DebugOverlay.
//   Exercises the pure DebugOverlayPresenter independently of its UI Toolkit Driver.
//
// KEY RESPONSIBILITIES:
//   - Verify run and movement formatting, precision limits, and invalid samples.
//   - Verify player teardown clears stale display values while retaining run status.
//
// DEPENDENCIES:
//   - Worsen.Presentation.DebugOverlay: Presenter and its passive DriverState.
//   - NUnit assertions and System.Globalization for a locale-isolation check.
//
// USAGE NOTES:
//   - Editor-only. Creates no engine objects; time and randomness are not involved.
//   - Any temporary current-culture change is restored even if an assertion fails.
//
// ============================================================================

using System.Globalization;
using NUnit.Framework;
using Worsen.Presentation.DebugOverlay;

namespace Worsen.Tests.DebugOverlay
{
    public sealed class DebugOverlayPresenterTests
    {
        [Test]
        public void InitialDisplayDoesNotInventPlayerTelemetry()
        {
            var state = new DebugOverlayDriverState();
            Assert.That(state.SpeedText, Is.EqualTo("Speed: —"));
            Assert.That(state.MovementText, Is.EqualTo("Movement: awaiting player (M1)"));
        }

        [Test]
        public void RunStatusRetainsFullTickRangeAndHandlesAbsentPhase()
        {
            var state = new DebugOverlayDriverState();
            var presenter = new DebugOverlayPresenter();
            presenter.SetRunStatus(state, long.MaxValue, "Running");
            Assert.That(state.TickText, Is.EqualTo("Tick: 9223372036854775807"));
            Assert.That(state.PhaseText, Is.EqualTo("Run: Running"));
            presenter.SetRunStatus(state, 0, "  ");
            Assert.That(state.PhaseText, Is.EqualTo("Run: unknown"));
        }

        [Test]
        public void SpeedFormattingUsesInvariantCultureAndRequestedPrecision()
        {
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var state = new DebugOverlayDriverState();
                new DebugOverlayPresenter().SetPlayerStatus(state, 12.375f, "Slide", 2);
                Assert.That(state.SpeedText, Is.EqualTo("Speed: 12.38 m/s"));
                Assert.That(state.MovementText, Is.EqualTo("Movement: Slide"));
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        [TestCase(0f, "Speed: 0.00 m/s")]
        [TestCase(-1f, "Speed: —")]
        [TestCase(float.NaN, "Speed: —")]
        [TestCase(float.PositiveInfinity, "Speed: —")]
        [TestCase(float.NegativeInfinity, "Speed: —")]
        public void InvalidSpeedIsUnavailableWhileZeroRemainsARealSample(float speed, string expected)
        {
            var state = new DebugOverlayDriverState();
            new DebugOverlayPresenter().SetPlayerStatus(state, speed, null, 2);
            Assert.That(state.SpeedText, Is.EqualTo(expected));
            Assert.That(state.MovementText, Is.EqualTo("Movement: unknown"));
        }

        [TestCase(-100, "Speed: 12 m/s")]
        [TestCase(100, "Speed: 12.375 m/s")]
        public void PrecisionOutsideConfigRangeIsBounded(int decimalPlaces, string expected)
        {
            var state = new DebugOverlayDriverState();
            new DebugOverlayPresenter().SetPlayerStatus(state, 12.375f, "Ground", decimalPlaces);
            Assert.That(state.SpeedText, Is.EqualTo(expected));
        }

        [Test]
        public void PlayerUnavailableClearsStalePlayerDataAndPreservesRunStatus()
        {
            var state = new DebugOverlayDriverState();
            var presenter = new DebugOverlayPresenter();
            presenter.SetRunStatus(state, 900, "Running");
            presenter.SetPlayerStatus(state, 12f, "Air", 2);
            presenter.SetPlayerUnavailable(state);
            Assert.That(state.SpeedText, Is.EqualTo("Speed: —"));
            Assert.That(state.MovementText, Is.EqualTo("Movement: awaiting player (M1)"));
            Assert.That(state.TickText, Is.EqualTo("Tick: 900"));
            Assert.That(state.PhaseText, Is.EqualTo("Run: Running"));
        }
    }
}
