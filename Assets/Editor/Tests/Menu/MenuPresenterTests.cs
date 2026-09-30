// ============================================================================
// MenuPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies title/start and pause/resume acknowledgement with pure menu state.
//   Settings are immutable runtime overrides with sanitation, not writes to designer assets.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Menu.
// KEY RESPONSIBILITIES:
//   - Test click debounce, pause acknowledgement and complete sanitized preferences.
// DEPENDENCIES:
//   NUnit, Core and Menu pure presentation.
// USAGE NOTES:
//   No engine globals, time, configuration objects or Session dependencies.
// ============================================================================
using NUnit.Framework;
using Worsen.Core;
using Worsen.Presentation.Menu;
namespace Worsen.Tests.Menu
{
    public sealed class MenuPresenterTests
    {
        [Test]
        public void TitleBlocksPauseAndStartStaysPendingUntilRunAcknowledgement()
        {
            var state = new MenuDriverState(); var p = new MenuPresenter(); p.ShowTitle(state);
            Assert.That(p.TryTogglePause(state, out _), Is.False);
            Assert.That(p.TryStart(state), Is.True); Assert.That(p.TryStart(state), Is.False);
            Assert.That(state.TitleVisible, Is.True);
            p.SetRunState(state, true, false);
            Assert.That(state.TitleVisible || state.Pending || state.Paused, Is.False);
            Assert.That(p.TryStart(state), Is.False);
        }
        [Test]
        public void PauseAndResumeWaitForSimulationAcknowledgementAndCannotRunAfterDeath()
        {
            var state = new MenuDriverState(); var p = new MenuPresenter(); p.SetRunState(state, true, false);
            Assert.That(p.TryTogglePause(state, out bool pause), Is.True); Assert.That(pause, Is.True);
            Assert.That(state.Paused, Is.False); Assert.That(p.TryTogglePause(state, out _), Is.False);
            p.SetRunState(state, true, true); Assert.That(state.Paused, Is.True);
            Assert.That(p.TryTogglePause(state, out pause), Is.True); Assert.That(pause, Is.False);
            p.SetRunState(state, true, false); Assert.That(state.Paused, Is.False);
            p.SetRunState(state, false, false); Assert.That(p.TryTogglePause(state, out _), Is.False);
        }
        [Test]
        public void SettingsAreClampedRuntimeCopiesAndSaveFailureIsVisible()
        {
            var original = new PlayerSettingsRecord(1, .1f, false, 95, true, true, true, 1, 1, 1);
            var state = new MenuDriverState(); var p = new MenuPresenter(); p.SetSettings(state, original);
            p.SetRunState(state, true, true);
            Assert.That(p.EditSettings(state, new PlayerSettingsRecord(1, float.NaN, true, -1, false, false, false, 2, -1, float.PositiveInfinity)), Is.True);
            Assert.That(p.TryApply(state, out var value), Is.True);
            Assert.That(value.MouseSensitivity, Is.EqualTo(.1f)); Assert.That(value.FieldOfView, Is.EqualTo(1));
            Assert.That(value.MasterVolume, Is.EqualTo(1)); Assert.That(value.MusicVolume, Is.Zero);
            Assert.That(value.EffectsVolume, Is.EqualTo(1)); Assert.That(value.InvertY, Is.True);
            Assert.That(value.CameraTilt || value.CameraPunch || value.ReacquireBlur, Is.False);
            Assert.That(state.Settings, Is.EqualTo(original)); Assert.That(original.FieldOfView, Is.EqualTo(95));
            Assert.That(p.EditSettings(state, new PlayerSettingsRecord(2, 0, false, 80, true, true, true, 0, 0, 0)), Is.False);
            p.SetSaveResult(state, false, "Disk full"); Assert.That(state.Message, Does.Contain("not saved").And.Contain("Disk full"));
            p.SetSaveResult(state, true, ""); Assert.That(state.Message, Is.EqualTo("Settings saved."));
        }
    }
}
