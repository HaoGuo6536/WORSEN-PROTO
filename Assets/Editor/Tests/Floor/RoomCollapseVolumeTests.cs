// ============================================================================
// RoomCollapseVolumeTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the cosmetic hand command path without changing the hazard contract.
//   Supplied time and nearby observations must move the art while colliders stay fixed.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Floor.
// KEY RESPONSIBILITIES:
//   - Verify visible time-driven search, near-player strain and expiring observations.
//   - Preserve triggers, verify Closed blocking sweeps and keep fog inside its footprint.
// DEPENDENCIES:
//   - NUnit, Core, Floor and native Unity objects/physics queries.
// USAGE NOTES:
//   Native Edit Mode for coordinator. No independent Update or scene assets required.
//   Explicit Teardown returns query buffers before destroying the temporary owner.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Floor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class RoomCollapseVolumeTests
    {
        [Test]
        public void OwnerTimeAndObservationMoveArtWithoutChangingTheHazard()
        {
            var root = new GameObject("Collapse hand motion");
            var config = Worsen.Tests.Core.ShaderReferenceTestSetup.Create<FloorDriverConfig>();
            var material = new Material(config.SurfaceShader);
            var volume = root.AddComponent<RoomCollapseVolume>();
            var warning = root.AddComponent<FloorLumenGlow>();
            try
            {
                var center = new Vector3(57000f, 2f, 57000f);
                var room = new LevelRoom(1, center, new Vector3(12f, 4f, 12f));
                root.transform.position = center;
                warning.Configure(null, 1f, Color.white, 1f, false);
                volume.Configure(room, config, material, warning, boundaryReach: .7f);
                var sample = new RoomDestructionSample(1, RoomPhase.Closed, 1f);
                volume.ApplyDestruction(sample, 1f);
                var boundary = root.GetComponent<BoxCollider>();
                Vector3 size = boundary.size, triggerCenter = boundary.center;
                var hands = root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Shadow Hand ")).ToArray();
                Assert.That(hands.Length, Is.GreaterThan(0));
                var before = hands.Select(hand => hand.rotation).ToArray();
                volume.ApplyDestruction(sample, 1.5f);
                Assert.That(hands.Where((hand, index) => hand.rotation != before[index]).Count(), Is.GreaterThan(0));
                var idle = hands.Select(hand => hand.rotation).ToArray();
                volume.ObservePlayer(center + new Vector3(5f, -2f, 0f), default);
                volume.ApplyDestruction(sample, 1.5f);
                Assert.That(hands.Where((hand, index) => hand.rotation != idle[index]).Count(), Is.GreaterThan(0));
                volume.ApplyDestruction(sample, 1.5f);
                Assert.That(hands.Select(hand => hand.rotation).ToArray(), Is.EqualTo(idle), "Target observations expire after one supplied pose.");
                Assert.That(boundary.enabled && boundary.isTrigger, Is.True);
                Assert.That(boundary.size, Is.EqualTo(size)); Assert.That(boundary.center, Is.EqualTo(triggerCenter));
                Assert.That(volume.Phase, Is.EqualTo(RoomPhase.Closed));
                foreach (var hand in hands)
                { Assert.That(room.Bounds.Contains(hand.position), Is.True); Assert.That(hand.gameObject.activeSelf, Is.True); }
                Assert.That(root.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
            }
            finally
            { volume.Teardown(); warning.Teardown(); Object.DestroyImmediate(root); Object.DestroyImmediate(material); Object.DestroyImmediate(config); }
        }
        [Test]
        public void GrowingFrontAndClosedDoorwayHaveMatchingGeometryAndARealSweepBarrier()
        {
            var root = new GameObject("Closed doorway sweep");
            var config = Worsen.Tests.Core.ShaderReferenceTestSetup.Create<FloorDriverConfig>();
            var material = new Material(config.SurfaceShader);
            var volume = root.AddComponent<RoomCollapseVolume>();
            var warning = root.AddComponent<FloorLumenGlow>();
            try
            {
                var room = new LevelRoom(1, new Vector3(59000f, 2f, 59000f), new Vector3(12f, 4f, 12f));
                root.transform.position = room.Center; warning.Configure(null, 1f, Color.white, 1f, false);
                volume.Configure(room, config, material, warning, boundaryReach: 2.1f);
                var seals = root.GetComponentsInChildren<BoxCollider>(true).Where(c => c.name == "Closed fog seal").ToArray();
                Assert.That(seals.Length, Is.EqualTo(4));
                volume.ApplyDestruction(new RoomDestructionSample(1, RoomPhase.Encroaching, .5f), 8f);
                Physics.SyncTransforms(); Assert.That(seals.All(c => !c.enabled), Is.True);
                var fog = root.GetComponentsInChildren<Renderer>(true).Single(r => r.name == "Advancing consuming fog");
                Assert.That(fog.bounds.max.x, Is.EqualTo(room.Bounds.min.x + 12f * .56f).Within(.01f));
                Assert.That(volume.Probe(new Vector3(fog.bounds.max.x + 1f, 0f, room.Center.z)).Available, Is.True);
                volume.ApplyDestruction(new RoomDestructionSample(1, RoomPhase.Closed, 0f), 10f);
                Physics.SyncTransforms(); Assert.That(seals.All(c => c.enabled && !c.isTrigger), Is.True);
                foreach (var direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                {
                    Vector3 origin = room.Center + direction * 8f;
                    Assert.That(Physics.SphereCast(origin, .35f, -direction, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore), Is.True);
                    Assert.That(seals, Does.Contain(hit.collider));
                    Assert.That(Vector3.Dot(hit.normal, direction), Is.GreaterThan(.99f));
                    Assert.That(hit.distance, Is.LessThan(2f), "A capsule cannot cross the doorway plane.");
                }
                Assert.That(fog.bounds.min.x, Is.EqualTo(room.Bounds.min.x).Within(.01f));
                Assert.That(fog.bounds.max.x, Is.EqualTo(room.Bounds.max.x).Within(.01f));
                volume.Teardown(); Assert.That(seals.All(c => !c.enabled), Is.True);
            }
            finally
            { volume.Teardown(); warning.Teardown(); Object.DestroyImmediate(root); Object.DestroyImmediate(material); Object.DestroyImmediate(config); }
        }
    }
}
