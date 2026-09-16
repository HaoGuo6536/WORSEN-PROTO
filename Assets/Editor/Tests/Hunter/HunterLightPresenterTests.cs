// ============================================================================
// HunterLightPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies Hunter behavior with explicit reproducible fixtures.
//   Tests exercise observable light, physical attacks, route admission or creature
//   animation contracts without changing authored gameplay assets.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10), test suite (section 11) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Keep per-life state separate from shared configuration and foreign systems.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Coordinator runs Unity tests with the exclusive lease. Fixtures clean up their own objects.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    public sealed class HunterLightPresenterTests
    {
        private readonly HunterLightPresenter _presenter = new HunterLightPresenter();
        private static FlashlightSample Sample(bool enabled = true, long tick = 10, float range = 12f)
            => new FlashlightSample(new EntityId(1), tick, enabled, Vector3.zero, Vector3.forward, range, 40f);
        [Test] public void DisabledFutureExpiredAndInvalidSamplesAreRejected()
        {
            Assert.That(_presenter.IsFresh(Sample(), 12, 4), Is.True);
            Assert.That(_presenter.IsFresh(Sample(false), 12, 4), Is.False);
            Assert.That(_presenter.IsFresh(Sample(tick: 13), 12, 4), Is.False);
            Assert.That(_presenter.IsFresh(Sample(), 15, 4), Is.False);
            Assert.That(_presenter.IsFresh(Sample(range: float.NaN), 12, 4), Is.False);
        }
        [Test] public void IlluminationRequiresConeAndRangeWhileRearPointIsExcluded()
        {
            Assert.That(_presenter.InBeam(Sample(), Vector3.forward * 12f), Is.True);
            Assert.That(_presenter.InBeam(Sample(), Vector3.forward * 12.01f), Is.False);
            Assert.That(_presenter.InBeam(Sample(), Quaternion.Euler(0, 21f, 0) * Vector3.forward * 5f), Is.False);
            Assert.That(_presenter.InBeam(Sample(), Vector3.back), Is.False);
        }
        [Test] public void ReflectedLightPatchStillRequiresHuntersOwnSightCone()
        {
            Assert.That(_presenter.InSight(Vector3.zero, Vector3.forward, Vector3.forward * 8f, 10f, 90f), Is.True);
            Assert.That(_presenter.InSight(Vector3.zero, Vector3.forward, Vector3.right * 8f, 10f, 90f), Is.False);
        }
    }
}
