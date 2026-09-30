// ============================================================================
// HorrorEffectsAllocationTests.cs
// ============================================================================
// PURPOSE:
//   Prove retained throw/visibility rays preserve the nearest distance/ID result.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · HorrorEffects physics integration.
// KEY RESPONSIBILITIES:
//   - Require repeated saturation to return every legacy hit.
//   - Compare impact/visibility with the sorted allocating oracle including ties.
// DEPENDENCIES:
//   NUnit, Unity physics, HorrorEffectsDriver and test-only seeded layout helpers.
// USAGE NOTES:
//   Edit Mode engine test; creates and destroys only isolated local objects.
// ============================================================================
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Session.HorrorEffects;
using Worsen.Tests.Hunter;

namespace Worsen.Tests.HorrorEffects
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HorrorEffectsAllocationTests
    {
        [TestCase(7)] [TestCase(77)]
        public void SaturatedImpactAndVisibilityMatchLegacyNearestHitWithIdTies(int seed)
        {
            using (var layout = new PhysicsAllocationLayout())
            {
                var driver = layout.Root.AddComponent<HorrorEffectsDriver>();
                int capacity = PhysicsAllocationLayout.ReplaceBuffer<RaycastHit>(driver, "QueryHits");
                layout.CastBoxes(capacity * 2 + 1, seed); Physics.SyncTransforms();
                RaycastHit[] expected = Physics.RaycastAll(layout.Origin, Vector3.forward, PhysicsAllocationLayout.Distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                Assert.That(expected.Length, Is.EqualTo(capacity * 2 + 1));
                Array.Sort(expected, (a, b) => { int distance = a.distance.CompareTo(b.distance);
                    return distance != 0 ? distance : a.collider.GetInstanceID().CompareTo(b.collider.GetInstanceID()); });
                layout.ExpectGrowth("Horror effects", 2);
                Vector3 target = layout.Origin + Vector3.forward * PhysicsAllocationLayout.Distance;
                Assert.That(driver.Impact(layout.Origin, target, layout.Root, out Vector3 point), Is.True);
                Assert.That(point, Is.EqualTo(expected[0].point));
                RaycastHit[] buffer = PhysicsAllocationLayout.Buffer<RaycastHit>(driver, "QueryHits");
                Assert.That(buffer.Length, Is.GreaterThan(capacity));
                Assert.That(buffer.Take(expected.Length).Select(hit => hit.collider), Is.EquivalentTo(expected.Select(hit => hit.collider)));
                Assert.That(driver.Visible(layout.Origin, target, layout.Root, expected[0].collider.gameObject), Is.True);
                Assert.That(driver.Visible(layout.Origin, target, layout.Root, expected[1].collider.gameObject), Is.False);
                layout.KeepFirst(4); Physics.SyncTransforms();
                Assert.That(driver.Impact(layout.Origin, target, layout.Root, out _), Is.True);
                Assert.That(buffer, Is.SameAs(PhysicsAllocationLayout.Buffer<RaycastHit>(driver, "QueryHits")));
                LogAssert.NoUnexpectedReceived();
            }
        }
    }
}
