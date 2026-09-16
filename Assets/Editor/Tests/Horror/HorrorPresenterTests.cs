// ============================================================================
// HorrorPresenterTests.cs
// ============================================================================
//
// PURPOSE:
//   Verifies darkness and warning math without loading a scene or polling enemy state.
//   Explicit samples cover multiplier effects, finite output, lamp resets and windup sound edges.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Horror.
//
// KEY RESPONSIBILITIES:
//   - Prove fog and flashlight modifiers affect their real distance outputs.
//   - Prove enemy warning timing, geometry and phase-triggered sound decisions.
//
// DEPENDENCIES:
//   - HorrorPresenter and its states/settings; Core attack samples; NUnit.
//
// USAGE NOTES:
//   Pure Edit Mode tests. Native Unity lighting and audible playback are separate checks.
//
// ============================================================================

using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Horror;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Horror
{
    public sealed class HorrorPresenterTests
    {
        private HorrorPresenter _presenter;
        private HorrorDriverState _state;
        private HorrorPresentationSettings _settings;
        [SetUp]
        public void SetUp()
        {
            _presenter = new HorrorPresenter();
            _state = new HorrorDriverState();
            _settings = new HorrorPresentationSettings
            {
                FogNearMeters = 8f, FogFarMeters = 24f, FlashlightRange = 18f, FlashlightIntensity = 7.5f,
                AttackRadius = 0.62f, WindupStartScale = 1.3f, WindupEndScale = 0.55f,
                AttackArrowLength = 2.1f, AttackHeight = 0.15f,
                WindupColor = new Color(1f, 0.72f, 0.25f, 1f),
                ActiveColor = new Color(1f, 0.1f, 0.035f, 1f),
                RecoveryColor = new Color(0.45f, 0.22f, 0.08f, 0.65f)
            };
        }

        [Test]
        public void BaseFogDistancesNormalizeAgainstCameraFarClip()
        {
            _presenter.CalculateAtmosphere(_state, _settings, 100f);
            Assert.That(_state.FogCurveStart, Is.EqualTo(0.08f).Within(0.000001f));
            Assert.That(_state.FogCurveEnd, Is.EqualTo(0.24f).Within(0.000001f));
            Assert.That(_state.FlashlightRange, Is.EqualTo(18f));
            Assert.That(_state.FlashlightIntensity, Is.EqualTo(7.5f));
        }

        [Test]
        public void MultipliersChangeBothFogDistanceAndFlashlightReach()
        {
            _presenter.SetEffects(_state, 2f, 0.75f);
            _presenter.CalculateAtmosphere(_state, _settings, 100f);
            Assert.That(_state.FogCurveStart, Is.EqualTo(0.04f).Within(0.000001f));
            Assert.That(_state.FogCurveEnd, Is.EqualTo(0.12f).Within(0.000001f));
            Assert.That(_state.FlashlightRange, Is.EqualTo(13.5f));
            Assert.That(_state.FlashlightIntensity, Is.EqualTo(7.5f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(0f)]
        [TestCase(-1f)]
        public void InvalidMultipliersUseNeutralValues(float value)
        {
            _presenter.SetEffects(_state, value, value);
            _presenter.CalculateAtmosphere(_state, _settings, 100f);
            Assert.That(_state.FogMultiplier, Is.EqualTo(1f));
            Assert.That(_state.FlashlightMultiplier, Is.EqualTo(1f));
            Assert.That(_state.FlashlightRange, Is.EqualTo(18f));
        }

        [Test]
        public void MalformedDistancesStillProduceAnOrderedFiniteFogInterval()
        {
            _settings.FogNearMeters = float.PositiveInfinity;
            _settings.FogFarMeters = float.NaN;
            _settings.FlashlightRange = float.NegativeInfinity;
            _settings.FlashlightIntensity = float.NaN;
            _presenter.CalculateAtmosphere(_state, _settings, float.NaN);
            Assert.That(_state.FogCurveStart, Is.InRange(0f, 0.999f));
            Assert.That(_state.FogCurveEnd, Is.GreaterThan(_state.FogCurveStart).And.LessThanOrEqualTo(1f));
            Assert.That(_state.FlashlightRange, Is.InRange(0.01f, 100f));
            Assert.That(_state.FlashlightIntensity, Is.EqualTo(0f));
        }

        [Test]
        public void BrightUpgradeCannotIlluminateBeyondTheCameraFarPlane()
        {
            _presenter.SetEffects(_state, 0.5f, 100f);
            _presenter.CalculateAtmosphere(_state, _settings, 30f);
            Assert.That(_state.FlashlightRange, Is.EqualTo(30f));
            Assert.That(_state.FogCurveStart, Is.LessThan(_state.FogCurveEnd));
        }

        [Test]
        public void RoundResetTurnsLampOnAndClearsAttacksButRetainsRunModifiers()
        {
            _presenter.SetEffects(_state, 2f, 0.75f);
            _state.Attacks.Add(new EntityId(1), new HorrorAttackDriverState { Phase = 1 });
            _presenter.ToggleFlashlight(_state);
            Assert.That(_state.FlashlightEnabled, Is.False);
            _presenter.ResetRound(_state);
            Assert.That(_state.FlashlightEnabled, Is.True);
            Assert.That(_state.Attacks, Is.Empty);
            Assert.That(_state.FogMultiplier, Is.EqualTo(2f));
            Assert.That(_state.FlashlightMultiplier, Is.EqualTo(0.75f));
        }

        [Test]
        public void WindupPlaysOneGrowlAndTheNextAttackRearmsIt()
        {
            var attack = new HorrorAttackDriverState();
            Assert.That(Present(attack, 1, 0f).PlayGrowl, Is.True);
            Assert.That(Present(attack, 1, 0.5f).PlayGrowl, Is.False);
            Assert.That(Present(attack, 1, 1f).PlayGrowl, Is.False);
            Assert.That(Present(attack, 2, 0f).PlayGrowl, Is.False);
            Assert.That(Present(attack, 3, 0f).PlayGrowl, Is.False);
            Assert.That(Present(attack, 0, 0f).Visible, Is.False);
            Assert.That(Present(attack, 1, 0f).PlayGrowl, Is.True);
        }

        [Test]
        public void EnemyWarningStateIsIndependentPerHunter()
        {
            var first = new HorrorAttackDriverState();
            var second = new HorrorAttackDriverState();
            Present(first, 1, 0f);
            Assert.That(Present(first, 1, 0.5f).PlayGrowl, Is.False);
            Assert.That(Present(second, 1, 0.5f).PlayGrowl, Is.True);
        }

        [Test]
        public void WindupContractsRingAndArrivesAtTheRedAttackColor()
        {
            var attack = new HorrorAttackDriverState();
            HorrorAttackVisual start = Present(attack, 1, 0f);
            HorrorAttackVisual end = Present(attack, 1, 1f);
            Assert.That(start.Visible, Is.True);
            Assert.That(end.Radius, Is.LessThan(start.Radius));
            Assert.That(end.Color, Is.EqualTo(_settings.ActiveColor));
            Assert.That(end.ArrowLength, Is.EqualTo(_settings.AttackArrowLength));
        }

        [Test]
        public void ArrowUsesHorizontalCommittedAttackDirection()
        {
            var sample = new HunterAttackSample(new EntityId(1), new Vector3(4f, 1f, 8f),
                new Vector3(3f, 10f, 4f), 1, 0.5f);
            HorrorAttackVisual visual = _presenter.PresentAttack(new HorrorAttackDriverState(), sample, _settings);
            Vector3 forward = visual.Rotation * Vector3.forward;
            Assert.That(Vector3.Distance(forward, new Vector3(0.6f, 0f, 0.8f)), Is.LessThan(0.00001f));
            Assert.That(visual.Position.y, Is.EqualTo(1.15f).Within(0.00001f));
        }

        [Test]
        public void StationaryAttackUsesAStableForwardArrow()
        {
            var sample = new HunterAttackSample(new EntityId(1), Vector3.zero, Vector3.zero, 1, 0f);
            HorrorAttackVisual visual = _presenter.PresentAttack(new HorrorAttackDriverState(), sample, _settings);
            Assert.That(visual.Rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(visual.Visible, Is.True);
        }

        [Test]
        public void InvalidIdentityOrPoseCannotEmitAVisibleOrAudibleWarning()
        {
            var missingId = new HunterAttackSample(EntityId.None, Vector3.zero, Vector3.forward, 1, 0f);
            var invalidPose = new HunterAttackSample(new EntityId(1), new Vector3(float.NaN, 0f, 0f),
                Vector3.forward, 1, 0f);
            foreach (HunterAttackSample sample in new[] { missingId, invalidPose })
            {
                HorrorAttackVisual visual = _presenter.PresentAttack(new HorrorAttackDriverState(), sample, _settings);
                Assert.That(visual.Visible, Is.False);
                Assert.That(visual.PlayGrowl, Is.False);
            }
        }

        [Test]
        public void CompletedRecoveryAndUnknownPhasesHideWarnings()
        {
            var attack = new HorrorAttackDriverState();
            Assert.That(Present(attack, 3, 1f).Visible, Is.False);
            Assert.That(Present(attack, 99, 0f).Visible, Is.False);
        }

        [TestCase(0, 8)]
        [TestCase(32, 32)]
        [TestCase(1000, 64)]
        public void RingIsFiniteClosedByRendererAndHasBoundedResolution(int requested, int expected)
        {
            Vector3[] ring = _presenter.BuildRing(requested);
            Assert.That(ring.Length, Is.EqualTo(expected));
            foreach (Vector3 point in ring)
            {
                Assert.That(point.y, Is.EqualTo(0f));
                Assert.That(point.magnitude, Is.EqualTo(1f).Within(0.00001f));
            }
            Assert.That(Vector3.Distance(ring[0], ring[ring.Length - 1]), Is.GreaterThan(0.01f));
        }

        [Test]
        public void ArrowContainsTheTipWingsAndShaft()
        {
            Vector3[] arrow = _presenter.BuildArrow(0.25f, 0.25f);
            Assert.That(arrow.Length, Is.EqualTo(5));
            Assert.That(arrow[0], Is.EqualTo(new Vector3(-0.25f, 0f, 0.75f)));
            Assert.That(arrow[2], Is.EqualTo(new Vector3(0.25f, 0f, 0.75f)));
            Assert.That(arrow[1], Is.EqualTo(Vector3.forward));
            Assert.That(arrow[4], Is.EqualTo(Vector3.zero));
        }

        private HorrorAttackVisual Present(HorrorAttackDriverState state, int phase, float progress)
        {
            return _presenter.PresentAttack(state,
                new HunterAttackSample(new EntityId(1), Vector3.zero, Vector3.forward, phase, progress), _settings);
        }
    }
}

