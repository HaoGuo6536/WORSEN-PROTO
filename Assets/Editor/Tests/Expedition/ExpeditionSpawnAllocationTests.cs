// ============================================================================
// ExpeditionSpawnAllocationTests.cs
// ============================================================================
// PURPOSE:
//   Prove complete pooled cover-ray queries independently of navigation admission.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Expedition physics integration.
// KEY RESPONSIBILITIES:
//   - Compare all hits with the legacy ray query at repeated saturation.
//   - Verify a shorter follow-up query reuses storage without stale prefix entries.
// DEPENDENCIES:
//   NUnit, Unity physics, ExpeditionSpawnDriver and test-only seeded layout helpers.
// USAGE NOTES:
//   Edit Mode engine test; private-query reflection avoids baking navigation or assets.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Session.Expedition;
using Worsen.Tests.Hunter;

namespace Worsen.Tests.Expedition
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ExpeditionSpawnAllocationTests
    {
        [TestCase(7)] [TestCase(77)]
        public void SaturatedCoverQueryRetriesAndReturnsEveryLegacyHit(int seed)
        {
            using (var layout = new PhysicsAllocationLayout())
            {
                var driver = layout.Root.AddComponent<ExpeditionSpawnDriver>();
                int capacity = PhysicsAllocationLayout.ReplaceBuffer<RaycastHit>(driver, "QueryHits");
                layout.CastBoxes(capacity * 2 + 1, seed); Physics.SyncTransforms();
                RaycastHit[] expected = Physics.RaycastAll(layout.Origin, Vector3.forward, PhysicsAllocationLayout.Distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                Assert.That(expected.Length, Is.EqualTo(capacity * 2 + 1));
                layout.ExpectGrowth("Expedition spawn", 2);
                int count = (int)PhysicsAllocationLayout.Call(driver, "RayQuery", layout.Origin, Vector3.forward, PhysicsAllocationLayout.Distance);
                RaycastHit[] buffer = PhysicsAllocationLayout.Buffer<RaycastHit>(driver, "QueryHits");
                Assert.That(buffer.Length, Is.GreaterThan(capacity));
                Assert.That(count, Is.EqualTo(expected.Length));
                Assert.That(buffer.Take(count).Select(hit => hit.collider), Is.EquivalentTo(expected.Select(hit => hit.collider)));
                layout.KeepFirst(4); Physics.SyncTransforms();
                count = (int)PhysicsAllocationLayout.Call(driver, "RayQuery", layout.Origin, Vector3.forward, PhysicsAllocationLayout.Distance);
                Assert.That(count, Is.EqualTo(4));
                Assert.That(buffer, Is.SameAs(PhysicsAllocationLayout.Buffer<RaycastHit>(driver, "QueryHits")));
                Assert.That(buffer.Take(count).Select(hit => hit.collider), Is.EquivalentTo(layout.LegacyCast("Ray").Select(hit => hit.collider)));
                LogAssert.NoUnexpectedReceived();
            }
        }
    }
}
