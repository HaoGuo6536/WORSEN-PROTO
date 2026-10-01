// ============================================================================
// HorrorAtmosphereAllocationTests.cs
// ============================================================================
// PURPOSE:
//   Prove complete pooled obstruction queries without activating vendor rendering.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Horror atmosphere physics integration.
// KEY RESPONSIBILITIES:
//   - Exercise exact ray and repeated overlap saturation against allocating oracles.
//   - Require all hits and unchanged storage on a shorter follow-up query.
// DEPENDENCIES:
//   NUnit, Unity physics, HorrorAtmosphereDriver/State and test-only layout helpers.
// USAGE NOTES:
//   Injects passive query state only; no ownership, Volume, camera or global render changes.
// ============================================================================
using System.Buffers;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Presentation.Horror;
using Worsen.Tests.Hunter;

namespace Worsen.Tests.Horror
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HorrorAtmosphereAllocationTests
    {
        [TestCase(7)] [TestCase(77)]
        public void ExactRaySaturationRetriesAndReturnsEveryLegacyHit(int seed)
        {
            using (var layout = new PhysicsAllocationLayout())
            {
                var driver = Driver(layout);
                int capacity = PhysicsAllocationLayout.Buffer<RaycastHit>(driver, "BeamHits").Length;
                layout.CastBoxes(capacity, seed); Physics.SyncTransforms();
                RaycastHit[] expected = Physics.RaycastAll(layout.Origin, Vector3.forward, PhysicsAllocationLayout.Distance,
                    ~0, QueryTriggerInteraction.Ignore);
                Assert.That(expected.Length, Is.EqualTo(capacity));
                layout.ExpectGrowth("Horror atmosphere", 1);
                int count = (int)PhysicsAllocationLayout.Call(driver, "BeamQuery", layout.Origin, Vector3.forward, PhysicsAllocationLayout.Distance);
                RaycastHit[] buffer = PhysicsAllocationLayout.Buffer<RaycastHit>(driver, "BeamHits");
                Assert.That(count, Is.EqualTo(expected.Length));
                Assert.That(buffer.Length, Is.GreaterThan(capacity));
                Assert.That(buffer.Take(count).Select(hit => hit.collider), Is.EquivalentTo(expected.Select(hit => hit.collider)));
                layout.KeepFirst(4); Physics.SyncTransforms();
                count = (int)PhysicsAllocationLayout.Call(driver, "BeamQuery", layout.Origin, Vector3.forward, PhysicsAllocationLayout.Distance);
                Assert.That(count, Is.EqualTo(4));
                Assert.That(buffer, Is.SameAs(PhysicsAllocationLayout.Buffer<RaycastHit>(driver, "BeamHits")));
                LogAssert.NoUnexpectedReceived();
            }
        }
        [TestCase(7)] [TestCase(77)]
        public void RepeatedNearOverlapSaturationReturnsEveryCollider(int seed)
        {
            using (var layout = new PhysicsAllocationLayout())
            {
                var driver = Driver(layout);
                int capacity = PhysicsAllocationLayout.Buffer<Collider>(driver, "NearColliders").Length;
                layout.OverlapBoxes(capacity * 2 + 1, seed); Physics.SyncTransforms();
                Collider[] expected = Physics.OverlapSphere(layout.Origin, PhysicsAllocationLayout.Radius, ~0, QueryTriggerInteraction.Ignore);
                Assert.That(expected.Length, Is.EqualTo(capacity * 2 + 1));
                layout.ExpectGrowth("Horror atmosphere", 2);
                int count = (int)PhysicsAllocationLayout.Call(driver, "NearQuery", layout.Origin, PhysicsAllocationLayout.Radius);
                Collider[] buffer = PhysicsAllocationLayout.Buffer<Collider>(driver, "NearColliders");
                Assert.That(count, Is.EqualTo(expected.Length));
                Assert.That(buffer.Length, Is.GreaterThan(capacity));
                Assert.That(buffer.Take(count), Is.EquivalentTo(expected));
                layout.KeepFirst(4); Physics.SyncTransforms();
                count = (int)PhysicsAllocationLayout.Call(driver, "NearQuery", layout.Origin, PhysicsAllocationLayout.Radius);
                Assert.That(count, Is.EqualTo(4));
                Assert.That(buffer, Is.SameAs(PhysicsAllocationLayout.Buffer<Collider>(driver, "NearColliders")));
                LogAssert.NoUnexpectedReceived();
            }
        }
        private static HorrorAtmosphereDriver Driver(PhysicsAllocationLayout layout)
        {
            var driver = layout.Root.AddComponent<HorrorAtmosphereDriver>();
            PhysicsAllocationLayout.Set(driver, "_state", new HorrorAtmosphereDriverState {
                BeamHits = ArrayPool<RaycastHit>.Shared.Rent(16), NearColliders = ArrayPool<Collider>.Shared.Rent(16) });
            return driver;
        }
    }
}
