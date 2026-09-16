// ============================================================================
// HunterAttackPresenterTests.cs
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
    public sealed class HunterAttackPresenterTests
    {
        [Test] public void SplitCurseCreatesThreeDistinctTravelDirections()
        {
            var presenter = new HunterAttackPresenter();
            Assert.That(presenter.Directions(Vector3.zero, Vector3.forward * 10f, false).Length, Is.EqualTo(1));
            Vector3[] directions = presenter.Directions(Vector3.zero, Vector3.forward * 10f, true);
            Assert.That(directions.Length, Is.EqualTo(3));
            Assert.That(Vector3.Angle(directions[0], directions[1]), Is.EqualTo(14f).Within(0.001f));
            Assert.That(Vector3.Angle(directions[1], directions[2]), Is.EqualTo(14f).Within(0.001f));
        }
        [Test] public void ThornRingRetainsCenterAndSixSeparatedPositionsOnSameFloor()
        {
            var presenter = new HunterAttackPresenter(); var target = new Vector3(3f, 4f, 8f);
            Vector3[] points = presenter.GroundPoints(target, 1f, true);
            Assert.That(points.Length, Is.EqualTo(7)); Assert.That(points[0], Is.EqualTo(target));
            for (int i = 1; i < points.Length; i++)
            { Assert.That(points[i].y, Is.EqualTo(4f)); Assert.That(Vector3.Distance(points[i], target), Is.EqualTo(1.8f).Within(0.001f)); }
        }
    }
}
