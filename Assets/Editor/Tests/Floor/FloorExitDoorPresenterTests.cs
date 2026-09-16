// ============================================================================
// FloorExitDoorPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies smooth opening and deliberate plane-crossing semantics without engine calls.
//   The doorway distinguishes collecting the last cake from deliberately leaving.
//   Explicit timing and crossing observations keep the transition reproducible.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Keep visible door movement and physical passage in agreement.
//   - Prevent a stationary overlap from becoming an accidental floor transition.
// DEPENDENCIES:
//   - Core shared values and Floor-owned visual configuration only.
// USAGE NOTES:
//   Scene-owned through FloorDriver. Session supplies elapsed time; no Update loop.
//   No global settings. Reinitialization clears crossing and opening state.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Floor;
namespace Worsen.Tests.Floor
{
    public sealed class FloorExitDoorPresenterTests
    {
        private readonly FloorExitDoorPresenter _presenter = new FloorExitDoorPresenter();
        private readonly Vector3 _size = new Vector3(2f,3f,2f);
        [Test] public void DoorHasBoundedSmoothOpeningAndOnlyReachesClearAngleAtEnd()
        {
            Assert.That(_presenter.OpeningProgress(-1f,1.2f),Is.Zero);
            Assert.That(_presenter.OpeningProgress(1.2f,1.2f),Is.EqualTo(1f));
            Assert.That(_presenter.HingeAngle(0f,100f),Is.Zero);
            Assert.That(_presenter.HingeAngle(0.5f,100f),Is.EqualTo(50f).Within(0.001f));
            Assert.That(_presenter.HingeAngle(1f,100f),Is.EqualTo(100f));
        }
        [TestCase(-0.5f)] [TestCase(0f)] [TestCase(0.5f)]
        public void StationaryOccupantNeverCompletes(float z)
        {
            var state=new FloorExitCrossingDriverState();
            for(int i=0;i<50;i++)
                Assert.That(_presenter.ObserveCrossing(state,new Vector3(0f,1f,z),_size,0.35f),Is.False);
        }
        [TestCase(-1f)] [TestCase(1f)]
        public void DeliberateCrossingFromEitherSideCompletesOnce(float side)
        {
            var state=new FloorExitCrossingDriverState();
            Assert.That(_presenter.ObserveCrossing(state,new Vector3(0f,1f,side*0.6f),_size,0.35f),Is.False);
            Assert.That(_presenter.ObserveCrossing(state,new Vector3(0f,1f,0f),_size,0.35f),Is.False);
            Assert.That(_presenter.ObserveCrossing(state,new Vector3(0f,1f,-side*0.6f),_size,0.35f),Is.True);
            Assert.That(_presenter.ObserveCrossing(state,new Vector3(0f,1f,side*0.6f),_size,0.35f),Is.False);
        }
        [Test] public void SmallPlaneJitterDoesNotCountAsCrossing()
        {
            var state=new FloorExitCrossingDriverState();
            for(int i=0;i<30;i++)
                Assert.That(_presenter.ObserveCrossing(state,new Vector3(0f,1f,i%2==0?-0.1f:0.1f),_size,0.35f),Is.False);
        }
        [Test] public void WalkingAroundTheJambCannotRetainAnArmedCrossing()
        {
            var state=new FloorExitCrossingDriverState();
            _presenter.ObserveCrossing(state,new Vector3(0f,1f,-0.6f),_size,0.35f);
            Assert.That(_presenter.ObserveCrossing(state,new Vector3(1.5f,1f,0f),_size,0.35f),Is.False);
            Assert.That(_presenter.ObserveCrossing(state,new Vector3(0f,1f,0.6f),_size,0.35f),Is.False);
        }
        [Test] public void AboveDoorOrInvalidCoordinatesCannotComplete()
        {
            var state=new FloorExitCrossingDriverState();
            _presenter.ObserveCrossing(state,new Vector3(0f,1f,-0.6f),_size,0.35f);
            Assert.That(_presenter.ObserveCrossing(state,new Vector3(0f,4f,0.6f),_size,0.35f),Is.False);
            Assert.That(_presenter.ObserveCrossing(state,new Vector3(float.NaN,1f,0.6f),_size,0.35f),Is.False);
        }
    }
}
