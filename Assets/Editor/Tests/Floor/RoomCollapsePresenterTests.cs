// ============================================================================
// RoomCollapsePresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies clipped collapse geometry and phase presentation without engine calls.
//   Explicit observations and elapsed time keep room hazards reproducible.
//   Room-local ownership prevents effects or contacts leaking across portals.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) Â· Domain Â· Floor.
// KEY RESPONSIBILITIES:
//   - Keep collapse presentation aligned with the staged gameplay hazard.
//   - Preserve one escape opportunity and exactly one hit per committed grab.
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
    public sealed class RoomCollapsePresenterTests
    {
        private readonly RoomCollapsePresenter _presenter = new RoomCollapsePresenter();
        private readonly Bounds _room = new Bounds(new Vector3(0f,3.5f,0f),new Vector3(12f,7f,12f));
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
        [Test] public void FissurePatternIsDeterministicAndHasRealJaggedSegments()
        {
            var a=_presenter.CrackPath(Vector3.zero,Vector3.right,Vector3.forward,10f,3);
            var b=_presenter.CrackPath(Vector3.zero,Vector3.right,Vector3.forward,10f,3);
            CollectionAssert.AreEqual(a,b); Assert.That(a.Length,Is.EqualTo(9));
            Assert.That(a[2].z,Is.Not.EqualTo(a[3].z));
        }
    }
}
