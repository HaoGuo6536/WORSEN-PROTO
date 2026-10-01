// ============================================================================
// HUDPresenterTests.cs
// ============================================================================
//
// PURPOSE:
//   Verifies presentation-only HUD behavior with primitive samples and explicit time.
//   These tests protect chase chrome suppression, interrupted restoration, missing direction
//   samples and the fixed three-slot contract without opening a Unity scene.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · HUD.
//
// KEY RESPONSIBILITIES:
//   - Verify half-second restore timing and immediate chase interruption.
//   - Verify remaining-only counts and keep invalid samples from becoming direction arrows.
//   - Keep guidance visible through chases and keep all formatted text chase-neutral.
//
// DEPENDENCIES:
//   - Worsen.Presentation.HUD, NUnit and Unity value types.
//
// USAGE NOTES:
//   - Editor-only pure tests; no engine objects, time globals or scene operations.
//
// ============================================================================

using NUnit.Framework;
using UnityEngine;
using Worsen.Presentation.HUD;
using Worsen.Core;

namespace Worsen.Tests.HUD
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HUDPresenterTests
    {
        [Test]
        public void HealthTracksHitsAndRegenerationThroughChaseAndModalSuppression()
        {
            var p = new HUDPresenter(); var s = new HUDDriverState(); var g = new HUDGuidancePresenter();
            p.SetDirection(s, Vector3.forward, true); p.SetCount(s, 1, 4);
            p.SetHealth(s, 100f, 100f); Assert.That(s.HealthFraction, Is.EqualTo(1f));
            p.SetChaseMode(s, true); p.SetHealth(s, 42.5f, 100f);
            Assert.That(s.HealthFraction, Is.EqualTo(.425f)); Assert.That(s.HealthText, Is.EqualTo("42.5 / 100"));
            p.SetModalOpen(s, true); p.SetHealth(s, 60f, 100f);
            Assert.That(s.HealthText, Is.EqualTo("60 / 100")); Assert.That(s.HealthKnown, Is.True);
            Assert.That(g.ArrowVisible(s), Is.False); Assert.That(p.TryShowPhantomCake(s, 1f), Is.False);
            p.SetChaseMode(s, false); p.Tick(s, 1f, .5f); Assert.That(g.CountVisible(s), Is.False);
            p.ResetRunView(s); Assert.That(s.ModalOpen, Is.True, "Capture reset must not reopen chrome over a modal.");
            p.SetModalOpen(s, false); Assert.That(g.CountVisible(s), Is.True);
        }

        [TestCase(float.NaN, 100f)] [TestCase(10f, 0f)] [TestCase(10f, float.PositiveInfinity)]
        public void InvalidHealthNeverClaimsAFullBar(float current, float maximum)
        {
            var s = new HUDDriverState(); var p = new HUDPresenter(); p.SetHealth(s, 100f, 100f); p.SetHealth(s, current, maximum);
            Assert.That(s.HealthKnown, Is.False); Assert.That(s.HealthFraction, Is.Zero); Assert.That(s.HealthText, Is.EqualTo("— / —"));
        }

        [Test]
        public void HeadingRotatesWorldCueAndExitStateRetainsOnlyTheFact()
        {
            var state = new HUDDriverState();
            var presenter = new HUDPresenter();
            presenter.SetDirection(state, Vector3.forward, true);
            presenter.SetHeading(state, 90f);
            Assert.That(state.DirectionDegrees, Is.EqualTo(-90f).Within(0.0001f));
            presenter.SetDirection(state, Vector3.right, true);
            Assert.That(state.DirectionDegrees, Is.Zero.Within(0.0001f));
            presenter.SetHeading(state, float.NaN);
            Assert.That(state.DirectionDegrees, Is.Zero.Within(0.0001f));
            presenter.SetExitState(state, ExitState.Open);
            Assert.That(state.ExitOpen, Is.True);
            presenter.SetExitState(state, ExitState.Locked);
            Assert.That(state.ExitOpen, Is.False);
        }

        [Test]
        public void ChaseLeavesEssentialFactsIntactAndHidesAllExtrasImmediately()
        {
            var state = new HUDDriverState();
            var presenter = new HUDPresenter();
            presenter.SetCount(state, 3, 7);
            presenter.SetItemSlots(state, 2, 8);
            presenter.SetDirection(state, Vector3.forward, true);
            presenter.SetChaseMode(state, true);
            presenter.Tick(state, 1f, 0.5f);
            Assert.That(state.CountText, Is.EqualTo("4"));
            Assert.That(state.DisplayedSlots, Is.EqualTo(3));
            Assert.That(state.DirectionVisible, Is.True);
            Assert.That(state.ExtraOpacity, Is.Zero);
            Assert.That(state.ChaseMode, Is.True);
            Assert.That(state.ChromeVisible, Is.False);
            Assert.That(state.ViewDirection, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void LostChaseRestoresOverHalfSecondAndRepeatedLostFactDoesNotRestart()
        {
            var state = new HUDDriverState();
            var presenter = new HUDPresenter();
            presenter.SetChaseMode(state, true);
            presenter.SetChaseMode(state, false);
            presenter.Tick(state, 0.25f, 0.5f);
            Assert.That(state.ExtraOpacity, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(state.ChromeVisible, Is.True);
            presenter.SetChaseMode(state, false);
            presenter.Tick(state, 0.25f, 0.5f);
            Assert.That(state.ExtraOpacity, Is.EqualTo(1f));
        }

        [Test]
        public void NewChaseCancelsPartialRestoreAndNextLossStartsFromHidden()
        {
            var state = new HUDDriverState();
            var presenter = new HUDPresenter();
            presenter.SetChaseMode(state, true);
            presenter.SetChaseMode(state, false);
            presenter.Tick(state, 0.3f, 0.5f);
            presenter.SetChaseMode(state, true);
            Assert.That(state.ExtraOpacity, Is.Zero);
            Assert.That(state.ChromeVisible, Is.False);
            presenter.SetChaseMode(state, false);
            presenter.Tick(state, 0.1f, 0.5f);
            Assert.That(state.ExtraOpacity, Is.EqualTo(0.2f).Within(0.0001f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        public void InvalidTimeDoesNotCorruptRestoration(float deltaTime)
        {
            var state = new HUDDriverState { ExtraOpacity = 0.3f };
            new HUDPresenter().Tick(state, deltaTime, 0.5f);
            Assert.That(state.ExtraOpacity, Is.EqualTo(0.3f));
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        [TestCase(-1f)]
        public void InvalidOrZeroDurationRestoresImmediately(float duration)
        {
            var state = new HUDDriverState { ExtraOpacity = 0f };
            new HUDPresenter().Tick(state, 0.1f, duration);
            Assert.That(state.ExtraOpacity, Is.EqualTo(1f));
        }

        [Test]
        public void InvalidOrUnavailableDirectionClearsPriorArrow()
        {
            var state = new HUDDriverState();
            var presenter = new HUDPresenter();
            presenter.SetDirection(state, Vector3.right, true);
            Assert.That(state.DirectionDegrees, Is.EqualTo(90f).Within(0.0001f));
            presenter.SetDirection(state, Vector3.zero, true);
            Assert.That(state.DirectionVisible, Is.False);
            presenter.SetDirection(state, new Vector3(float.NaN, 0f, 1f), true);
            Assert.That(state.DirectionVisible, Is.False);
            presenter.SetDirection(state, Vector3.forward, false);
            Assert.That(state.DirectionVisible, Is.False);
            Assert.That(state.DirectionDegrees, Is.Zero);
        }

        [Test]
        public void CountsPreserveAuthoritativeNumbersAndEmptySlotsAreBounded()
        {
            var state = new HUDDriverState();
            var presenter = new HUDPresenter();
            presenter.SetCount(state, 0, 0);
            Assert.That(state.CountText, Is.EqualTo("0"));
            presenter.SetCount(state, -1, 4);
            Assert.That(state.CountText, Is.EqualTo("—"));
            presenter.SetItemSlots(state, 11, 8);
            Assert.That(state.DisplayedSlots, Is.EqualTo(3));
            Assert.That(state.SlotOverflowText, Is.Empty);
            presenter.SetHeldItemCount(state, 11, 8);
            Assert.That(state.DisplayedSlots, Is.EqualTo(3));
            Assert.That(state.SlotOverflowText, Is.EqualTo("+8"));
            presenter.SetItemSlots(state, -2, 8);
            Assert.That(state.DisplayedSlots, Is.EqualTo(3));
            Assert.That(state.SlotOverflowText, Is.Empty);
        }

        [Test]
        public void DisplayGaugeIsBoundedWithoutChangingAuthoritativeCountText()
        {
            var state = new HUDDriverState();
            var presenter = new HUDPresenter();
            presenter.SetCount(state, 7, 4);
            Assert.That(state.CountText, Is.EqualTo("0"));
            Assert.That(state.CountKnown, Is.True);
            Assert.That(state.CountFraction, Is.EqualTo(1f));
            presenter.SetCount(state, 0, 0);
            Assert.That(state.CountKnown, Is.True);
            Assert.That(state.CountFraction, Is.Zero);
            presenter.SetCount(state, -1, 4);
            Assert.That(state.CountFraction, Is.Zero);
            presenter.SetCount(state, 1, 4);
            Assert.That(state.CountFraction, Is.EqualTo(0.25f));
        }
        [Test]
        public void NewRunImmediatelyClearsChaseAndPartialRestorationWithoutErasingDisplayFacts()
        {
            var presenter = new HUDPresenter();
            var state = new HUDDriverState();
            presenter.SetCount(state, 2, 6);
            presenter.SetExitState(state, ExitState.Locked);
            presenter.SetDirection(state, Vector3.forward, true);
            presenter.SetChaseMode(state, true);
            presenter.ResetRunView(state);
            Assert.That(state.ChaseMode, Is.False);
            Assert.That(state.ExtraOpacity, Is.EqualTo(1f));
            Assert.That(state.DirectionVisible, Is.True);
            Assert.That(state.ChromeVisible, Is.True);
            Assert.That(state.CountText, Is.EqualTo("4"));
            Assert.That(state.ExitOpen, Is.False);

            presenter.SetChaseMode(state, true);
            presenter.SetChaseMode(state, false);
            presenter.Tick(state, .1f, 1f);
            Assert.That(state.ExtraOpacity, Is.EqualTo(.1f).Within(.0001f));
            presenter.ResetRunView(state);
            presenter.ResetRunView(state);
            Assert.That(state.ChaseMode, Is.False);
            Assert.That(state.ExtraOpacity, Is.EqualTo(1f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PresenterTextNeverReportsChaseState(bool chasing)
        {
            var state = new HUDDriverState(); var presenter = new HUDPresenter();
            presenter.SetCount(state, 3, 7);
            presenter.SetExitState(state, ExitState.Open);
            presenter.SetItemSlots(state, 11, 8);
            presenter.SetDirection(state, Vector3.forward, true);
            presenter.SetChaseMode(state, chasing);
            foreach (string text in new[] { state.CountText, state.HealthText, state.SlotOverflowText })
                Assert.That(text, Does.Not.Contain("HUNTED"));
            Assert.That(state.DirectionVisible, Is.True);
            Assert.That(state.ChromeVisible, Is.EqualTo(!chasing));
        }

        [Test]
        public void CompassRetainsDirectlyAboveBelowAndBehindTargets()
        {
            var state = new HUDDriverState(); var presenter = new HUDPresenter();
            presenter.SetDirection(state, Vector3.up, true);
            Assert.That(state.DirectionVisible, Is.True);
            Assert.That(state.DirectionPitchDegrees, Is.EqualTo(90f).Within(.001f));
            presenter.SetDirection(state, Vector3.down, true);
            Assert.That(state.DirectionVisible, Is.True);
            Assert.That(state.DirectionPitchDegrees, Is.EqualTo(-90f).Within(.001f));
            presenter.SetDirection(state, Vector3.back, true);
            Assert.That(Mathf.Abs(state.DirectionDegrees), Is.EqualTo(180f).Within(.001f));
            Assert.That(state.ViewDirection.z, Is.LessThan(0f));
        }

        [Test]
        public void FreeLookRotationSupersedesMovementHeadingAndPreservesCameraPitch()
        {
            var state = new HUDDriverState(); var presenter = new HUDPresenter();
            presenter.SetDirection(state, Vector3.forward, true);
            // Camera looks right while locomotion heading remains forward.
            presenter.SetViewRotation(state, new Quaternion(0f, .70710678f, 0f, .70710678f));
            presenter.SetHeading(state, 0f);
            Assert.That(state.DirectionDegrees, Is.EqualTo(-90f).Within(.001f));
            // Camera looks upward30 degrees; the level target is now below its aim.
            presenter.SetViewRotation(state, new Quaternion(-.25881905f, 0f, 0f, .9659258f));
            Assert.That(state.DirectionPitchDegrees, Is.EqualTo(-30f).Within(.001f));
        }

        [Test]
        public void InvalidCameraSamplesAndExtremeFiniteDirectionsRemainSafe()
        {
            var state = new HUDDriverState(); var presenter = new HUDPresenter();
            presenter.SetViewRotation(state, new Quaternion(0f, 0f, 0f, 2f));
            presenter.SetDirection(state, new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), true);
            Assert.That(state.ViewDirection.magnitude, Is.EqualTo(1f).Within(.001f));
            presenter.SetViewRotation(state, new Quaternion(float.NaN, 0f, 0f, 1f));
            presenter.SetViewRotation(state, new Quaternion(0f, 0f, 0f, 0f));
            Assert.That(state.ViewRotation.w, Is.EqualTo(1f));
            presenter.SetDirection(state, Vector3.up, false);
            Assert.That(state.DirectionVisible, Is.False);
            Assert.That(state.ViewDirection, Is.EqualTo(Vector3.zero));
        }

    }
}
