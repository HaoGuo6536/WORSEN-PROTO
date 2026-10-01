// ============================================================================
// BlinderProjectilePresenterTests.cs
// ============================================================================
// PURPOSE:
//   Checks the cosmetic flight independently of Unity rendering and physics.
//   These tests do not claim that launch admission or gameplay collision changed.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Horror.
// KEY RESPONSIBILITIES:
//   - Verify injected-time travel, range expiry, malformed input and hit identity.
// DEPENDENCIES:
//   Core Blinder facts, Horror Presenter/DriverState, NUnit and Unity value types.
// USAGE NOTES:
//   Pure tests; no engine objects, wall clock, asset access or singleton state.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Horror;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Horror
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class BlinderProjectilePresenterTests
    {
        private static BlinderThrowFact Launch(float speed = 12f, float range = 16f)
            => new BlinderThrowFact(new EntityId(7), 1, 2, Vector3.zero, Vector3.forward, .06f, speed, range);
        [Test] public void FlightUsesInjectedTimeAndExpiresAtRangeNotTargetDistance()
        {
            var presenter = new BlinderProjectilePresenter(); var state = presenter.Create(Launch());
            Assert.That(presenter.Tick(state, .25f), Is.True);
            Assert.That(state.Position.z, Is.EqualTo(3f)); Assert.That(state.PreviousPosition, Is.EqualTo(Vector3.zero));
            Assert.That(presenter.Tick(state, .25f), Is.True);
            Assert.That(state.Position.z, Is.EqualTo(6f)); Assert.That(state.PreviousPosition.z, Is.EqualTo(3f));
            Assert.That(presenter.Tick(state, 100f), Is.False); Assert.That(state.Position.z, Is.EqualTo(16f));
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidOrPausedTimeDoesNotMove(float delta)
        {
            var presenter = new BlinderProjectilePresenter(); var state = presenter.Create(Launch());
            Assert.That(presenter.Tick(state, delta), Is.True); Assert.That(state.Traveled, Is.Zero);
            Assert.That(state.Position, Is.EqualTo(Vector3.zero));
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidLaunchSpeedOrRangeDoesNotCreateVisual(float value)
        {
            var presenter = new BlinderProjectilePresenter();
            Assert.That(presenter.Create(Launch(speed: value)), Is.Null);
            Assert.That(presenter.Create(Launch(range: value)), Is.Null);
        }
        [Test] public void ZeroDirectionAndNonFinitePositionsAreRejected()
        {
            var presenter = new BlinderProjectilePresenter();
            Assert.That(presenter.Create(new BlinderThrowFact(new EntityId(7), 1, 2, Vector3.zero, Vector3.zero, .06f, 12f, 16f)), Is.Null);
            Assert.That(presenter.Create(new BlinderThrowFact(new EntityId(7), 1, 2, new Vector3(float.NaN, 0, 0), Vector3.one, .06f, 12f, 16f)), Is.Null);
        }
        [Test] public void HitRequiresHunterAndSerialAndNeverConsumesTrap()
        {
            var presenter = new BlinderProjectilePresenter(); var state = presenter.Create(Launch());
            Assert.That(presenter.Matches(state, Hit(7, 2)), Is.True);
            Assert.That(presenter.Matches(state, Hit(8, 2)), Is.False);
            Assert.That(presenter.Matches(state, Hit(7, 3)), Is.False);
            Assert.That(presenter.Matches(state, Hit(7, 2, true)), Is.False);
        }
        private static BlinderHitFact Hit(int hunter, int serial, bool trap = false)
            => new BlinderHitFact(new EntityId(hunter), new EntityId(1), 3, serial, 3f, false, trap);
    }
}
