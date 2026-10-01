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
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
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
        [Test]
        public void DeliberateFrontToBackCrossingCompletesOnce()
        {
            var state=new FloorExitCrossingDriverState();
            Assert.That(_presenter.ObserveCrossing(state,new Vector3(0f,1f,-0.6f),_size,0.35f),Is.False);
            Assert.That(_presenter.ObserveCrossing(state,new Vector3(0f,1f,0f),_size,0.35f),Is.False);
            Assert.That(_presenter.ObserveCrossing(state,new Vector3(0f,1f,0.6f),_size,0.35f),Is.True);
            Assert.That(_presenter.ObserveCrossing(state,new Vector3(0f,1f,-0.6f),_size,0.35f),Is.False);
        }
        [Test] public void BackToFrontDoesNotCompleteButCanTurnAndEnterTheEscape()
        {
            var state = new FloorExitCrossingDriverState();
            foreach (float z in new[] { .6f, 0f, -.6f })
                Assert.That(_presenter.ObserveCrossing(state, new Vector3(0f, 1f, z), _size, .35f), Is.False);
            Assert.That(_presenter.ObserveCrossing(state, new Vector3(0f, 1f, .6f), _size, .35f), Is.True);
        }
        [Test] public void LockedOpeningOpenAndResetTransitionsAreOneShotAndClearContacts()
        {
            var state = new FloorExitDoorDriverState();
            _presenter.Tick(state, 20f, 1.2f);
            Assert.That(state.Opening || state.FullyOpen, Is.False);
            Assert.That(state.Elapsed, Is.Zero);
            state.Contacts.Add(1, new FloorExitCrossingDriverState { EntrySide = -1 });
            Assert.That(_presenter.Open(state), Is.True);
            Assert.That(state.Contacts, Is.Empty);
            _presenter.Tick(state, 20.6f, 1.2f);
            Assert.That(state.Opening, Is.True); Assert.That(state.FullyOpen, Is.False);
            Assert.That(_presenter.Open(state), Is.False);
            Assert.That(state.Elapsed, Is.EqualTo(.6f).Within(.00001f));
            state.Contacts.Add(1, new FloorExitCrossingDriverState { EntrySide = -1 });
            _presenter.Tick(state, 21.3f, 1.2f);
            Assert.That(state.FullyOpen, Is.True); Assert.That(state.Opening, Is.False);
            Assert.That(state.Contacts, Is.Empty); Assert.That(_presenter.Open(state), Is.False);
            _presenter.Reset(state);
            Assert.That(state.FullyOpen || state.Opening, Is.False);
            Assert.That(state.Elapsed, Is.Zero); Assert.That(state.LastClock, Is.Zero);
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)]
        public void InvalidOrRewoundClockCannotFastForwardOpening(float clock)
        {
            var state = new FloorExitDoorDriverState();
            _presenter.Tick(state, 5f, 1f); _presenter.Open(state);
            _presenter.Tick(state, clock, 1f); _presenter.Tick(state, 5.25f, 1f);
            Assert.That(state.Elapsed, Is.EqualTo(.25f)); Assert.That(state.FullyOpen, Is.False);
        }
        [TestCase(0f)] [TestCase(-.1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidCrossingDistanceFailsClosed(float distance)
        {
            var state = new FloorExitCrossingDriverState { EntrySide = -1 };
            Assert.That(_presenter.ObserveCrossing(state, new Vector3(0f, 1f, .6f), _size, distance), Is.False);
            Assert.That(state.EntrySide, Is.Zero);
        }
        [TestCase(.9f, 1f)] [TestCase(-.9f, 1f)] [TestCase(1f, 1f)] [TestCase(-1f, 1f)]
        [TestCase(0f, 3f)] [TestCase(0f, -.1f)]
        public void JambAndVerticalBoundaryCannotComplete(float x, float y)
        {
            var state = new FloorExitCrossingDriverState { EntrySide = -1 };
            Assert.That(_presenter.ObserveCrossing(state, new Vector3(x, y, .6f), _size, .35f), Is.False);
            Assert.That(state.EntrySide, Is.Zero);
        }
        [TestCase(2f, .899f, true)] [TestCase(2f, .9f, false)]
        [TestCase(4f, 1.899f, true)] [TestCase(4f, 1.9f, false)]
        public void AuthoredApertureAndItsScaledCentreInsetAgree(float width, float x, bool completes)
        {
            foreach (float sign in new[] { -1f, 1f })
            {
                var state = new FloorExitCrossingDriverState { EntrySide = -1 };
                Assert.That(_presenter.ObserveCrossing(state, new Vector3(sign * x, 1f, .6f),
                    new Vector3(width, 3f, 2f), .35f), Is.EqualTo(completes));
            }
        }
        [Test] public void EasedSwingIsMonotonicWithSlowEndpoints()
        {
            float previous = 0f;
            for (int i = 0; i <= 100; i++)
            {
                float angle = _presenter.HingeAngle(i / 100f, 100f);
                Assert.That(angle, Is.InRange(previous, 100f)); previous = angle;
            }
            Assert.That(_presenter.HingeAngle(.01f, 100f), Is.LessThan(.1f));
            Assert.That(100f - _presenter.HingeAngle(.99f, 100f), Is.LessThan(.1f));
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
