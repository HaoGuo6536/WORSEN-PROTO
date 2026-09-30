// ============================================================================
// SettingsControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies runtime settings sanitation and monotonic best depth without storage.
//   Version rejection and lifetime counting are explicit so floor results cannot inflate runs.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Session · Settings.
// KEY RESPONSIBILITIES:
//   - Cover invalid numbers, unsupported schemas, best-depth ties and retained unlocks.
// DEPENDENCIES:
//   NUnit, Core records and Settings pure logic only.
// USAGE NOTES:
//   Pure EditMode tests; no Unity objects or file IO.
// ============================================================================
using NUnit.Framework;
using Worsen.Core;
using Worsen.Session.Settings;
namespace Worsen.Tests.Settings
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class SettingsControllerTests
    {
        private static PlayerSettingsRecord Defaults => new PlayerSettingsRecord(1, .1f, false, 95, true, true, true, 1, 1, 1);
        [Test]
        public void InvalidNumbersAreSanitizedAndUnsupportedVersionsLeaveRuntimeUntouched()
        {
            var state = new SettingsBehaviorState(); var controller = new SettingsController(state, Defaults);
            Assert.That(controller.Apply(new PlayerSettingsRecord(1, float.NaN, true, 200, false, false, false, -2, float.PositiveInfinity, 4)), Is.True);
            Assert.That(state.Settings.MouseSensitivity, Is.EqualTo(.1f));
            Assert.That(state.Settings.FieldOfView, Is.EqualTo(179));
            Assert.That(state.Settings.MasterVolume, Is.Zero);
            Assert.That(state.Settings.MusicVolume, Is.EqualTo(1));
            Assert.That(state.Settings.EffectsVolume, Is.EqualTo(1));
            Assert.That(state.Settings.InvertY, Is.True);
            Assert.That(state.Settings.CameraTilt || state.Settings.CameraPunch || state.Settings.ReacquireBlur, Is.False);
            Assert.That(controller.Apply(new PlayerSettingsRecord(2, 1, false, 70, true, true, true, 1, 1, 1)), Is.False);
            Assert.That(state.Settings.FieldOfView, Is.EqualTo(179));
        }
        [Test]
        public void BestDepthOnlyIncreasesAndRunsAreCountedSeparatelyWithUnlocksPreserved()
        {
            var state = new SettingsBehaviorState(); var controller = new SettingsController(state, Defaults);
            controller.RestoreHistory(new RunHistoryRecord(1, 8, 12, new[] { "echo", "echo", " " }));
            Assert.That(controller.RecordBestDepth(11), Is.False);
            Assert.That(controller.RecordBestDepth(12), Is.False);
            Assert.That(controller.RecordBestDepth(-1), Is.False);
            Assert.That(controller.RecordBestDepth(13), Is.True);
            Assert.That(state.History.BestDepth, Is.EqualTo(13));
            Assert.That(state.History.LifetimeRuns, Is.EqualTo(8));
            controller.RecordRunStarted();
            Assert.That(state.History.LifetimeRuns, Is.EqualTo(9));
            Assert.That(state.History.UnlockedThreatIds, Is.EqualTo(new[] { "echo" }));
            Assert.That(controller.RestoreHistory(new RunHistoryRecord(2, 0, 0, null)), Is.False);
            Assert.That(state.History.BestDepth, Is.EqualTo(13));
        }
        [Test]
        public void LifetimeCounterSaturatesInsteadOfWrapping()
        {
            var state = new SettingsBehaviorState(); var controller = new SettingsController(state, Defaults);
            controller.RestoreHistory(new RunHistoryRecord(1, long.MaxValue, 0, null));
            controller.RecordRunStarted(); Assert.That(state.History.LifetimeRuns, Is.EqualTo(long.MaxValue));
        }
    }
}
