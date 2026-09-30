// ============================================================================
// CueBudgetDefinitionsTests.cs
// ============================================================================
// PURPOSE:
//   Protects sound tells from accidental timing jitter in shared cue metadata.
//   Invalid ranges must fail before presentation attempts to randomize playback.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Core Audio.
// KEY RESPONSIBILITIES:
//   - Verify tell timing and finite, ordered variation bounds.
// DEPENDENCIES:
//   - Core cue definitions, System and NUnit only.
// USAGE NOTES:
//   Pure Edit Mode tests; fixture values are not production defaults.
// ============================================================================
using System;
using NUnit.Framework;
using Worsen.Core;

namespace Worsen.Tests.Core
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class CueBudgetDefinitionsTests
    {
        [Test]
        public void TimingTellForbidsJitterButAllowsPitchVariation()
        {
            Assert.Throws<ArgumentException>(() => new CueVariationSpec(0.9f, 1.1f, 0.1f, 0.01f, 2, true));
            var tell = new CueVariationSpec(0.9f, 1.1f, 0.1f, 0f, 2, true);
            Assert.That(tell.TimingIsTell, Is.True);
            Assert.That(tell.TimingJitter, Is.Zero);
            Assert.That(tell.MinimumPitch, Is.EqualTo(0.9f));
            Assert.That(tell.MaximumPitch, Is.EqualTo(1.1f));
            Assert.That(tell.AlternateCount, Is.EqualTo(2));
            Assert.That(new CueVariationSpec(1f, 1f, 0f, 0.1f, 0, false).TimingJitter, Is.EqualTo(0.1f));
        }

        [TestCase(0f, 1f, 0f, 0f, 0)]
        [TestCase(2f, 1f, 0f, 0f, 0)]
        [TestCase(1f, 1f, -0.1f, 0f, 0)]
        [TestCase(1f, 1f, 1.1f, 0f, 0)]
        [TestCase(1f, 1f, 0f, -0.1f, 0)]
        [TestCase(1f, 1f, 0f, 0f, -1)]
        [TestCase(float.NaN, 1f, 0f, 0f, 0)]
        [TestCase(1f, float.PositiveInfinity, 0f, 0f, 0)]
        [TestCase(1f, 1f, 0f, float.NaN, 0)]
        public void InvalidRangesAreRejected(float min, float max, float volume, float timing, int alternates) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new CueVariationSpec(min, max, volume, timing, alternates, false));
    }
}
