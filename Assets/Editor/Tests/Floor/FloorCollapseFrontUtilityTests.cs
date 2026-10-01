// ============================================================================
// FloorCollapseFrontUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Specifies a single continuous room front and inward-only Closed wall geometry.
//   The same samples exercise hand reach so visuals cannot silently diverge from grabs.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Floor.
// KEY RESPONSIBILITIES:
//   - Check monotonic phase mapping, all travel directions and footprint clipping.
//   - Check front reach, consumed interiors and doorway wall/bounce directions.
// DEPENDENCIES:
//   - NUnit, Core and Floor pure value calculations only.
// USAGE NOTES:
//   Headless pure tests. Collider sweeps and Player impulse routing need native tests.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
namespace Worsen.Tests.Floor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class FloorCollapseFrontUtilityTests
    {
        private static LevelRoom Room => new LevelRoom(1, new Vector3(0f, 2f, 0f), new Vector3(12f, 4f, 12f));
        [Test]
        public void ProgressIsContinuousAtEveryPhaseBoundaryAndNoFogAppearsDuringTelegraph()
        {
            float previous = 0f;
            foreach (var phase in new[] { RoomPhase.Open, RoomPhase.Telegraph, RoomPhase.Tearing, RoomPhase.Encroaching, RoomPhase.Closed })
                for (int step = 0; step <= 20; step++)
                {
                    float value = FloorCollapseFrontUtility.Consumption(phase, step / 20f);
                    Assert.That(value, Is.GreaterThanOrEqualTo(previous)); previous = value;
                    if (phase == RoomPhase.Open || phase == RoomPhase.Telegraph) Assert.That(value, Is.Zero);
                }
            Assert.That(previous, Is.EqualTo(1f));
            Assert.That(FloorCollapseFrontUtility.Consumption(RoomPhase.Tearing, 1f),
                Is.EqualTo(FloorCollapseFrontUtility.Consumption(RoomPhase.Encroaching, 0f)));
        }
        [TestCase(1, 0)] [TestCase(-1, 0)] [TestCase(0, 1)] [TestCase(0, -1)]
        public void PlaneClipsExactlyOneHalfAtMidTravelAndNeverProtrudes(int x, int z)
        {
            var direction = new Vector3(x, 0f, z);
            Assert.That(FloorCollapseFrontUtility.Direction(direction * 9f), Is.EqualTo(direction));
            int axis = x != 0 ? 0 : 2;
            float previous = 0f;
            for (int i = 0; i <= 100; i++)
            {
                float progress = i / 100f;
                float plane = FloorCollapseFrontUtility.Plane(Room.Bounds, direction, progress);
                bool exists = FloorCollapseFrontUtility.Clip(Room.Bounds, direction, plane, out var consumed);
                Assert.That(exists, Is.EqualTo(i > 0));
                Assert.That(consumed.size[axis], Is.EqualTo(12f * progress).Within(.0001f));
                Assert.That(consumed.size[axis], Is.GreaterThanOrEqualTo(previous)); previous = consumed.size[axis];
                Assert.That(consumed.min[axis], Is.GreaterThanOrEqualTo(-6f));
                Assert.That(consumed.max[axis], Is.LessThanOrEqualTo(6f));
            }
        }
        [Test]
        public void AdjacentCellsShareOneFrontAndTheNotchStaysEmpty()
        {
            var cells = new[] { Room.Bounds, new Bounds(new Vector3(12f, 2f, 0f), Room.Size),
                new Bounds(new Vector3(0f, 2f, 12f), Room.Size) };
            var room = new LevelRoom(2, new Vector3(6f, 2f, 6f), new Vector3(24f, 4f, 24f), cells);
            float plane = FloorCollapseFrontUtility.Plane(room.Bounds, Vector3.right, .25f);
            Assert.That(plane, Is.Zero);
            Assert.That(FloorCollapseFrontUtility.Clip(cells[1], Vector3.right, plane, out _), Is.False);
            Assert.That(FloorCollapseFrontUtility.Probe(room, Vector3.right, .7f, 1f, new Vector3(12f, 0f, 12f), 2.1f).Available, Is.False);
            // At a shared seam the logical hand is still the same front.
            var a = FloorCollapseFrontUtility.Probe(room, Vector3.right, .25f, 1f, new Vector3(1f, 0f, 5.9f), 2.1f);
            var b = FloorCollapseFrontUtility.Probe(room, Vector3.right, .25f, 1f, new Vector3(1f, 0f, 6.1f), 2.1f);
            Assert.That(a.Available && b.Available, Is.True); Assert.That(a.HandId, Is.EqualTo(b.HandId));
            var walls = FloorCollapseFrontUtility.ClosedWalls(room, .2f);
            Assert.That(walls.Any(w => w.min.x >= 5.8f && w.max.x <= 6.01f && w.min.z < 0f && w.max.z > 0f), Is.False,
                "Internal seams must not become hard walls.");
        }
        [Test]
        public void ReachTracksTheMovingFrontNotTheRoomPerimeter()
        {
            var near = FloorCollapseFrontUtility.Probe(Room, Vector3.right, .5f, 1f, Vector3.right, 2.1f);
            Assert.That(near.Available, Is.True); Assert.That(near.Position.x, Is.Zero); Assert.That(near.Distance, Is.EqualTo(1f));
            Assert.That(FloorCollapseFrontUtility.Probe(Room, Vector3.right, .5f, 1f, Vector3.right * 4f, 2.1f).Available, Is.False);
            var inside = FloorCollapseFrontUtility.Probe(Room, Vector3.right, .5f, 1f, Vector3.left * 4f, 2.1f);
            Assert.That(inside.Available, Is.True); Assert.That(inside.Distance, Is.Zero);
            Assert.That(FloorCollapseFrontUtility.Probe(Room, Vector3.right, .5f, 1f, Vector3.up * 4f, 2.1f).Available, Is.False);
        }
        [TestCase(0, -1)] [TestCase(0, 1)] [TestCase(2, -1)] [TestCase(2, 1)]
        public void ClosedWallSealsDoorwayPlaneAndBouncePointsOutward(int axis, int sign)
        {
            Vector3 normal = Vector3.zero; normal[axis] = sign;
            var wall = FloorCollapseFrontUtility.ClosedWalls(Room, .2f).Single(w =>
                w.size[axis] < 1f && w.center[axis] * sign > 5f);
            Assert.That(sign > 0 ? wall.max[axis] : wall.min[axis], Is.EqualTo(sign * 6f).Within(.0001f));
            Assert.That(wall.min[axis], Is.GreaterThanOrEqualTo(-6f)); Assert.That(wall.max[axis], Is.LessThanOrEqualTo(6f));
            var probe = new RoomCollapsePresenter().BoundaryProbe(Room, RoomPhase.Closed, normal * 6.5f, 2.1f);
            var impulse = FloorCollapseFrontUtility.Bounce(probe, .65f, 6f);
            Assert.That(Vector3.Dot(impulse, normal), Is.EqualTo(6f));
            Assert.That(Vector3.Dot(impulse, -normal), Is.LessThan(0f));
            Assert.That(FloorCollapseFrontUtility.Bounce(new FloorHandProbe(1, 0, Vector3.zero, .7f, true, normal, closed: true), .65f, 6f), Is.EqualTo(Vector3.zero));
        }
    }
}
