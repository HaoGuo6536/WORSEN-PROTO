// ============================================================================
// PersistenceUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Verifies history and settings sanitation before future Session persistence.
//   Tests preserve unknown schema versions and caller-owned collection isolation.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Core Persistence.
// KEY RESPONSIBILITIES:
//   - Check numeric clamps, nonfinite fallback and immutable unlock snapshots.
// DEPENDENCIES:
//   - Core persistence records, System and NUnit only.
// USAGE NOTES:
//   Pure Edit Mode tests without file operations; fallback values are fixtures.
// ============================================================================
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Worsen.Core;

namespace Worsen.Tests.Core
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class PersistenceUtilityTests
    {
        private static PlayerSettingsRecord Fallback() => new PlayerSettingsRecord(1, 1f, false, 90f, true, true, true, 1f, 0.5f, 0.75f);

        [Test]
        public void HistoryClampsCountersAndCanonicalizesWithoutRewritingIds()
        {
            var source = new[] { "z", "a", "a", null, " ", " A " };
            var record = new RunHistoryRecord(0, -2, -3, source);
            source[0] = "changed";
            var clean = PersistenceUtility.ClampHistory(record);
            Assert.That(clean.SchemaVersion, Is.EqualTo(1));
            Assert.That(clean.LifetimeRuns, Is.Zero);
            Assert.That(clean.BestDepth, Is.Zero);
            Assert.That(clean.UnlockedThreatIds, Is.EqualTo(new[] { " A ", "a", "z" }));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)clean.UnlockedThreatIds)[0] = "changed");
            Assert.That(PersistenceUtility.ClampHistory(default).UnlockedThreatIds, Is.Empty);
        }

        [Test]
        public void ValidHistoryAndFutureSchemaRemainUnchanged()
        {
            var history = PersistenceUtility.ClampHistory(new RunHistoryRecord(99, long.MaxValue, int.MaxValue, null));
            Assert.That(history.SchemaVersion, Is.EqualTo(99));
            Assert.That(history.LifetimeRuns, Is.EqualTo(long.MaxValue));
            Assert.That(history.BestDepth, Is.EqualTo(int.MaxValue));
            Assert.That(history.UnlockedThreatIds, Is.Empty);
        }

        [Test]
        public void SettingsClampFiniteRangesAndPreserveSwitches()
        {
            var clean = PersistenceUtility.ClampSettings(new PlayerSettingsRecord(0, -1f, true, 200f, false, true, false, -1f, 2f, 0.3f), Fallback());
            Assert.That(clean.SchemaVersion, Is.EqualTo(1));
            Assert.That(clean.MouseSensitivity, Is.Zero);
            Assert.That(clean.FieldOfView, Is.EqualTo(179f));
            Assert.That(clean.InvertY, Is.True);
            Assert.That(clean.CameraTilt, Is.False);
            Assert.That(clean.CameraPunch, Is.True);
            Assert.That(clean.ReacquireBlur, Is.False);
            Assert.That(clean.MasterVolume, Is.Zero);
            Assert.That(clean.MusicVolume, Is.EqualTo(1f));
            Assert.That(clean.EffectsVolume, Is.EqualTo(0.3f));
            Assert.That(PersistenceUtility.ClampSettings(default, Fallback()).FieldOfView, Is.EqualTo(1f));
        }

        [Test]
        public void NonfiniteSettingsUseExplicitFallbackAndFutureVersionIsPreserved()
        {
            var fallback = Fallback();
            var clean = PersistenceUtility.ClampSettings(new PlayerSettingsRecord(99, float.NaN, false, float.PositiveInfinity,
                true, true, true, float.NegativeInfinity, float.NaN, float.PositiveInfinity), fallback);
            Assert.That(clean.SchemaVersion, Is.EqualTo(99));
            Assert.That(clean.MouseSensitivity, Is.EqualTo(fallback.MouseSensitivity));
            Assert.That(clean.FieldOfView, Is.EqualTo(fallback.FieldOfView));
            Assert.That(clean.MasterVolume, Is.EqualTo(fallback.MasterVolume));
            Assert.That(clean.MusicVolume, Is.EqualTo(fallback.MusicVolume));
            Assert.That(clean.EffectsVolume, Is.EqualTo(fallback.EffectsVolume));
            Assert.Throws<ArgumentException>(() => PersistenceUtility.ClampSettings(clean, default));
            Assert.That(PersistenceUtility.ClampSettings(fallback, fallback), Is.EqualTo(fallback));
        }
    }
}
