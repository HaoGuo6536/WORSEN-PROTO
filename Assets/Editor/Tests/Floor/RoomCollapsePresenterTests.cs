// ============================================================================
// RoomCollapsePresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies clipped collapse geometry and phase presentation without engine calls.
//   Boundary tests cover doorway contact, penetration, escape and vertical isolation.
//   Explicit observations and elapsed time keep room hazards reproducible.
//   Room-local ownership prevents effects or contacts leaking across portals.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) Â· Domain Â· Floor.
// KEY RESPONSIBILITIES:
//   - Keep collapse presentation aligned with the staged gameplay hazard.
//   - Preserve one escape opportunity and exactly one hit per committed grab.
//   - Reject L-shaped notches and internal seams while retaining rectangle probes exactly.
// DEPENDENCIES:
//   - Core shared floor facts and Unity value types; no higher-layer dependency.
// USAGE NOTES:
//   Scene-owned through FloorManager/FloorDriver. Time is supplied by the owner.
//   No persistent singleton, global settings, or independent update loop.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
namespace Worsen.Tests.Floor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class RoomCollapsePresenterTests
    {
        private readonly RoomCollapsePresenter _presenter = new RoomCollapsePresenter();
        private readonly Bounds _room = new Bounds(new Vector3(0f,3.5f,0f),new Vector3(12f,7f,12f));
        [TestCase(RoomPhase.Tearing)] [TestCase(RoomPhase.Encroaching)] [TestCase(RoomPhase.Closed)]
        public void FootprintRejectsNotchAndUsesOnlyExposedEdges(RoomPhase phase)
        {
            var cells = new[] { _room, new Bounds(new Vector3(12f, 3.5f, 0f), _room.size),
                new Bounds(new Vector3(0f, 3.5f, 12f), _room.size) };
            var room = new LevelRoom(1, new Vector3(6f, 3.5f, 6f), new Vector3(24f, 7f, 24f), cells);
            Assert.That(_presenter.BoundaryProbe(room, phase, new Vector3(6.1f, 0f, 6.1f), 2.1f).Available, Is.False);
            Assert.That(_presenter.BoundaryProbe(room, phase, new Vector3(12f, 0f, 12f), 2.1f).Available, Is.False);
            var seam = _presenter.BoundaryProbe(room, phase, new Vector3(6f, 0f, 0f), 2.1f);
            Assert.That(seam.Available, Is.EqualTo(phase == RoomPhase.Closed));
            if (seam.Available) Assert.That(seam.Penetration, Is.EqualTo(6f));
            Assert.That(_presenter.BoundaryProbe(room, phase, new Vector3(5.5f, 0f, 12f), 2.1f).Available, Is.True);
            foreach (var cell in cells)
                for (int i = 0; i < 25; i++) Assert.That(room.ContainsXZ(_presenter.GridPoint(cell, i, 5, .25f)), Is.True);
        }

        [Test]
        public void DefaultRectangleProbeIsUnchangedIncludingExteriorReach()
        {
            var room = new LevelRoom(1, _room.center, _room.size);
            foreach (var phase in new[] { RoomPhase.Open, RoomPhase.Tearing, RoomPhase.Encroaching, RoomPhase.Closed })
                foreach (var position in new[] { Vector3.zero, Vector3.right * 5f, Vector3.right * 6.2f, Vector3.up * 7f })
                    Assert.That(_presenter.BoundaryProbe(room, phase, position, 2.1f),
                        Is.EqualTo(_presenter.BoundaryProbe(_room, 1, phase, position, 2.1f)));
        }
        [Test] public void GraspShapeOpensOnEscapeAndClosesOnGrab()
        {
            Assert.That(_presenter.GripWeight(CollapseHandEventKind.Warning),Is.EqualTo(30f));
            Assert.That(_presenter.GripWeight(CollapseHandEventKind.Grabbed),Is.EqualTo(100f));
            Assert.That(_presenter.GripWeight(CollapseHandEventKind.Escaped),Is.Zero);
        }
        [Test] public void MistWaitsForTearingThenProgressivelyConsumesRoom()
        {
            Assert.That(_presenter.MistProgress(RoomPhase.Telegraph,1f),Is.Zero);
            Assert.That(_presenter.MistProgress(RoomPhase.Tearing,1f),Is.EqualTo(0.12f));
            Assert.That(_presenter.MistProgress(RoomPhase.Encroaching,0.5f),Is.InRange(0.5f,0.7f));
            Assert.That(_presenter.MistProgress(RoomPhase.Closed,0f),Is.EqualTo(1f));
        }
        [Test] public void PortalAndVerticalBoundaryRejectNeighborsAndUpperSeparateRooms()
        {
            Assert.That(_presenter.Contains(_room,new Vector3(5.9f,0f,0f),0.25f),Is.False);
            Assert.That(_presenter.Contains(_room,new Vector3(6.1f,0f,0f),0.25f),Is.False);
            Assert.That(_presenter.Contains(_room,new Vector3(0f,7f,0f),0.25f),Is.False);
            Assert.That(_presenter.Contains(_room,new Vector3(0f,3f,0f),0.25f),Is.True);
        }
        [Test] public void GridStaysInsideRoomAndAvoidsDoorThresholds()
        {
            for(int i=0;i<25;i++)
                Assert.That(_presenter.Contains(_room,_presenter.GridPoint(_room,i,5,0.25f),0.25f),Is.True);
        }
        [Test] public void OuterHandsRevealBeforeCenterAndAllAreVisibleWhenConsumed()
        {
            var outer=_presenter.GridPoint(_room,0,5,0.25f);
            var center=_presenter.GridPoint(_room,12,5,0.25f);
            Assert.That(_presenter.HandReveal(_room,outer,0.3f),Is.GreaterThan(0f));
            Assert.That(_presenter.HandReveal(_room,center,0.3f),Is.Zero);
            Assert.That(_presenter.HandReveal(_room,center,1f),Is.EqualTo(1f).Within(0.0001f));
        }
        [Test] public void BoundaryReachesDoorwayAndTracksOutwardDirectionWithoutVisualHands()
        {
            var outside = _presenter.BoundaryProbe(_room, 1, RoomPhase.Closed, new Vector3(6.2f, 0f, 0f), 2.1f);
            Assert.That(outside.Available, Is.True); Assert.That(outside.Outward, Is.EqualTo(Vector3.right));
            Assert.That(outside.Distance, Is.EqualTo(0.2f).Within(0.001f)); Assert.That(outside.Penetration, Is.Zero);
            var inside = _presenter.BoundaryProbe(_room, 1, RoomPhase.Closed, new Vector3(4f, 0f, 0f), 2.1f, outside.HandId);
            Assert.That(inside.Penetration, Is.EqualTo(2f)); Assert.That(inside.Position.x, Is.EqualTo(6f));
            Assert.That(_presenter.BoundaryProbe(_room, 1, RoomPhase.Open, Vector3.zero, 2.1f).Available, Is.False);
            Assert.That(_presenter.BoundaryProbe(_room, 1, RoomPhase.Closed, Vector3.up * 7f, 2.1f).Available, Is.False);
            Assert.That(_presenter.BoundaryProbe(_room, 1, RoomPhase.Closed, Vector3.right * 8.2f, 2.1f).Available, Is.False);
        }
        [Test] public void CakeReachApproachesRewardAndPulseUsesPublishedCycle()
        {
            var cake = Vector3.right * 4f;
            var early = _presenter.CakeReach(Vector3.zero, cake, RoomPhase.Tearing, 0.5f);
            var late = _presenter.CakeReach(Vector3.zero, cake, RoomPhase.Encroaching, 0.5f);
            Assert.That(late.x, Is.GreaterThan(early.x));
            Assert.That(_presenter.CakeReach(Vector3.zero, cake, RoomPhase.Closed, 1f), Is.EqualTo(cake));
            Assert.That(_presenter.Pulse(new RoomDestructionSample(1, RoomPhase.Telegraph, 0f, 2f, 0.25f)), Is.EqualTo(1f));
            Assert.That(_presenter.Pulse(new RoomDestructionSample(1, RoomPhase.Open, 0f)), Is.Zero);
        }
        [TestCase(RoomPhase.Tearing)] [TestCase(RoomPhase.Encroaching)]
        public void CollapsingRoomGrabsAtBoundaryNotThroughoutItsClearInterior(RoomPhase phase)
        {
            Assert.That(_presenter.BoundaryProbe(_room, 1, phase, Vector3.right * 5f, 2.1f).Available, Is.True);
            Assert.That(_presenter.BoundaryProbe(_room, 1, phase, Vector3.zero, 2.1f).Available, Is.False);
            Assert.That(_presenter.BoundaryProbe(_room, 1, RoomPhase.Closed, Vector3.zero, 2.1f).Available, Is.True);
        }
        [Test] public void FissurePatternIsDeterministicAndHasRealJaggedSegments()
        {
            var a=_presenter.CrackPath(Vector3.zero,Vector3.right,Vector3.forward,10f,3);
            var b=_presenter.CrackPath(Vector3.zero,Vector3.right,Vector3.forward,10f,3);
            CollectionAssert.AreEqual(a,b); Assert.That(a.Length,Is.EqualTo(9));
            Assert.That(a[2].z,Is.Not.EqualTo(a[3].z));
        }
    }
}
