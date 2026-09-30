// ============================================================================
// PlayerWebSlowTests.cs
// ============================================================================
// PURPOSE:
//   Verifies timed web movement effects without another slow owner or engine tick.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Player.
// KEY RESPONSIBILITIES:
//   - Check strength composition, expiry, invalid contacts, overlaps and sliding.
// DEPENDENCIES:
//   - Core, Player pure logic, NUnit and a temporary profile.
// USAGE NOTES:
//   Coordinator-run Edit Mode tests. All time and movement observations are explicit.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Player
{
    public sealed class PlayerWebSlowTests
    {
        private PlayerProfile profile;
        private PlayerBehaviorState state;
        private PlayerController controller;
        [SetUp] public void Setup()
        {
            profile = ScriptableObject.CreateInstance<PlayerProfile>(); state = new PlayerBehaviorState();
            controller = new PlayerController(state, profile, new System.Random(1)); controller.Reset(new EntityId(1), Vector3.zero, 0f);
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(profile);
        private WebHitFact Hit(float slow = .5f, float duration = 1f, float strength = 1f) =>
            new WebHitFact(new EntityId(-1), state.Id, 0, 1, slow, duration, strength);
        [Test] public void CutterStrengthComposesWithGrabAndTrapAndExpiresIndependently()
        {
            controller.SetGrabSpeedMultiplier(.8f); controller.SetTrapSpeedMultiplier(.6f);
            state.Velocity = Vector3.forward * 100f; controller.ApplyWebSlow(Hit(strength: .5f));
            Assert.That(state.WebSpeedMultiplier, Is.EqualTo(.75f));
            Assert.That(state.Velocity.magnitude, Is.EqualTo(state.MaxDesignSpeed * .8f * .6f * .75f).Within(.0001f));
            controller.SetGrabSpeedMultiplier(1f); Assert.That(state.WebSpeedMultiplier, Is.EqualTo(.75f));
            controller.Tick(default, default, .5f, 1); Assert.That(state.WebSlowRemaining, Is.EqualTo(.5f));
            controller.Tick(default, default, .5f, 2); Assert.That(state.WebSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(state.TrapSpeedMultiplier, Is.EqualTo(.6f));
        }
        [Test] public void WebAllowsSlideAtScaledEntrySpeedAndResetClearsIt()
        {
            controller.ApplyWebSlow(Hit()); state.Velocity = Vector3.forward * profile.SlideMinimumSpeed * .5f;
            controller.Tick(new InputFrame(Vector2.up, Vector2.zero, InputButtons.Crouch, InputButtons.Crouch, InputButtons.None),
                new MovementProbe(true, Vector3.up), .02f, 1);
            Assert.That(state.MovementState, Is.EqualTo(MovementState.Slide));
            controller.Reset(state.Id, Vector3.zero, 0f);
            Assert.That(state.WebSlowRemaining, Is.Zero); Assert.That(state.WebSpeedMultiplier, Is.EqualTo(1f));
        }
        [Test] public void InvalidOrForeignHitsDoNotApplyAndOverlapsDoNotMultiply()
        {
            controller.ApplyWebSlow(Hit(duration: float.NaN)); controller.ApplyWebSlow(Hit(slow: float.PositiveInfinity));
            controller.ApplyWebSlow(new WebHitFact(new EntityId(-1), new EntityId(2), 0, 1, .1f, 5f, 1f));
            Assert.That(state.WebSlowRemaining, Is.Zero);
            controller.ApplyWebSlow(Hit(.4f, 2f)); controller.ApplyWebSlow(Hit(.8f, 3f));
            Assert.That(state.WebSpeedMultiplier, Is.EqualTo(.4f).Within(.0001f)); Assert.That(state.WebSlowRemaining, Is.EqualTo(3f));
        }
    }
}
