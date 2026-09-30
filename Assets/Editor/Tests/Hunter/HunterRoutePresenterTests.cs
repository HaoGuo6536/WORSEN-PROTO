// ============================================================================
// HunterRoutePresenterTests.cs
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
//   - Keep bounded emergence selection deterministic without removing collision-safe corners.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Coordinator runs Unity tests with the exclusive lease. Fixtures clean up their own objects.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Hunter;
namespace Worsen.Tests.Hunter
{
    public sealed class HunterRoutePresenterTests
    {
        [Test] public void EmergencePrefersLastHiddenDoorwayBeforeVisibilityWithoutChangingRoute()
        {
            var route = new[] { Vector3.zero, Vector3.forward, Vector3.one, Vector3.right * 3, Vector3.right * 5 };
            var copy = (Vector3[])route.Clone();
            var presenter = new HunterRoutePresenter();
            Assert.That(presenter.EmergenceCorner(route, new[] { true, true, false, true, false }, true, 16), Is.EqualTo(3));
            Assert.That(presenter.EmergenceCorner(route, new[] { true, true, false, true, false }, true, 3), Is.EqualTo(1));
            Assert.That(route, Is.EqualTo(copy));
            Assert.That(presenter.Allowed(route, null), Is.True);
        }
        [Test] public void NoOcclusionOrChaseLeavesOrdinaryCornerRoutingUnchanged()
        {
            var route = new[] { Vector3.zero, Vector3.forward, Vector3.one };
            var presenter = new HunterRoutePresenter();
            Assert.That(presenter.EmergenceCorner(route, new[] { false, false, false }, true, 16), Is.EqualTo(-1));
            Assert.That(presenter.EmergenceCorner(route, new[] { true, true, true }, true, 16), Is.EqualTo(-1));
            Assert.That(presenter.EmergenceCorner(route, new[] { true, true, false }, false, 16), Is.EqualTo(-1));
            Assert.That(presenter.EmergenceCorner(route, new[] { true, true, false }, true, 2), Is.EqualTo(-1));
            Assert.That(presenter.EmergenceCorner(null, null, true, 16), Is.EqualTo(-1));
        }
        [Test] public void APathCannotCrossConsumedRoomBetweenCorners()
        {
            var presenter = new HunterRoutePresenter(); var rooms = new[] { new Bounds(Vector3.zero, Vector3.one * 4f) };
            Assert.That(presenter.Allowed(new[] { Vector3.left * 4f, Vector3.right * 4f }, rooms), Is.False);
            Assert.That(presenter.Allowed(new[] { Vector3.left * 4f, Vector3.zero }, rooms), Is.False);
            Assert.That(presenter.Allowed(new[] { new Vector3(-4, 5, 0), new Vector3(4, 5, 0) }, rooms), Is.True);
        }
    }
}
