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
        [Test] public void APathCannotCrossConsumedRoomBetweenCorners()
        {
            var presenter = new HunterRoutePresenter(); var rooms = new[] { new Bounds(Vector3.zero, Vector3.one * 4f) };
            Assert.That(presenter.Allowed(new[] { Vector3.left * 4f, Vector3.right * 4f }, rooms), Is.False);
            Assert.That(presenter.Allowed(new[] { Vector3.left * 4f, Vector3.zero }, rooms), Is.False);
            Assert.That(presenter.Allowed(new[] { new Vector3(-4, 5, 0), new Vector3(4, 5, 0) }, rooms), Is.True);
        }
    }
}
