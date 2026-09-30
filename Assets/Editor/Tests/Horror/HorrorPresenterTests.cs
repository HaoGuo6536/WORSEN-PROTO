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
//   - Prove seeded startle count, spacing, run reset and default-off fog hooks.
//   - Prove injected run-clock accumulation, invalid-delta rejection and reset boundaries.
//
// DEPENDENCIES:
//   - HorrorPresenter, PostFX composition, Core attack samples, NUnit and editor config serialization.
//
// USAGE NOTES:
//   Pure Edit Mode tests. Native Unity lighting and audible playback are separate checks.
//
// ============================================================================

using NUnit.Framework;
using UnityEditor;
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
        private HorrorDriverConfig _config;
        [TearDown] public void TearDown() => Object.DestroyImmediate(_config);
        [Test]
        public void AuthoritativeFlashlightMatchesSensingAndRejectsStaleOrInvalidAim()
        {
            var first = new FlashlightSample(new EntityId(42), 10, true, Vector3.up, Vector3.forward, 23f, 28f);
            Assert.That(_presenter.SetFlashlight(_state, first), Is.True);
            _presenter.SetEffects(_state, 1f, 0.2f);
            _presenter.CalculateAtmosphere(_state, _settings, 100f);
            Assert.That(_state.FlashlightRange, Is.EqualTo(23f));
            _presenter.ToggleFlashlight(_state);
            Assert.That(_state.FlashlightEnabled, Is.True);
            Assert.That(_presenter.SetFlashlight(_state, new FlashlightSample(first.Source, 9, false, Vector3.up, Vector3.forward, 2f, 30f)), Is.False);
            Assert.That(_presenter.SetFlashlight(_state, new FlashlightSample(first.Source, 11, true, Vector3.up, Vector3.zero, 2f, 30f)), Is.False);
            _presenter.ResetRound(_state);
            Assert.That(_state.HasAuthoritativeFlashlight, Is.False);
        }

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<HorrorDriverConfig>();
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

        [Test]
        public void RunClockAccumulatesInjectedDeltasAndOnlyRunResetZeroesIt()
        {
            Assert.That(_state.RunElapsedSeconds, Is.Zero);
            Assert.That(_presenter.AdvanceRunClock(_state, 0.25f), Is.True);
            Assert.That(_presenter.AdvanceRunClock(_state, 0.5f), Is.True);
            Assert.That(_state.RunElapsedSeconds, Is.EqualTo(0.75d));
            _presenter.ResetRound(_state);
            Assert.That(_state.RunElapsedSeconds, Is.EqualTo(0.75d));
            Assert.That(_presenter.AdvanceRunClock(_state, 1.25f), Is.True);
            Assert.That(_state.RunElapsedSeconds, Is.EqualTo(2d));
            _presenter.ResetRun(_state);
            Assert.That(_state.RunElapsedSeconds, Is.Zero);
            Assert.That(_presenter.AdvanceRunClock(_state, 0.125f), Is.True);
            Assert.That(_state.RunElapsedSeconds, Is.EqualTo(0.125d));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(-1f)]
        public void InvalidRunClockDeltasAreRejectedWithoutChangingElapsedTime(float deltaSeconds)
        {
            _presenter.AdvanceRunClock(_state, 2f);
            Assert.That(_presenter.AdvanceRunClock(_state, deltaSeconds), Is.False);
            Assert.That(_state.RunElapsedSeconds, Is.EqualTo(2d));
        }

        [Test]
        public void ZeroDeltaIsAcceptedAndLargeFiniteDeltasUseDoubleAccumulation()
        {
            Assert.That(_presenter.AdvanceRunClock(_state, 0f), Is.True);
            Assert.That(_state.RunElapsedSeconds, Is.Zero);
            Assert.That(_presenter.AdvanceRunClock(_state, float.MaxValue), Is.True);
            Assert.That(_presenter.AdvanceRunClock(_state, float.MaxValue), Is.True);
            Assert.That(_state.RunElapsedSeconds, Is.EqualTo(2d * float.MaxValue));
        }

        [Test]
        public void StartleBudgetCountsSpacesAndSurvivesFloorButNotRunReset()
        {
            var random = new System.Random(22);
            Assert.That(_presenter.TryStartle(_state, _config, 0d, true, random), Is.True);
            Assert.That(_presenter.TryStartle(_state, _config, 119.99d, true, random), Is.False);
            Assert.That(_state.StartlesUsed, Is.EqualTo(1));
            _presenter.ResetRound(_state);
            Assert.That(_presenter.TryStartle(_state, _config, 120d, true, random), Is.True);
            Assert.That(_presenter.TryStartle(_state, _config, 240d, true, random), Is.False);
            Assert.That(_state.StartlesUsed, Is.EqualTo(2));
            _presenter.ResetRun(_state);
            Assert.That(_presenter.TryStartle(_state, _config, 0d, true, new System.Random(22)), Is.True);
        }

        [Test]
        public void UnearnedInvalidDuplicateOrRewoundRequestsDoNotSpendBudget()
        {
            var random = new System.Random(22);
            foreach (double time in new[] { double.NaN, double.PositiveInfinity, -1d })
                Assert.That(_presenter.TryStartle(_state, _config, time, true, random), Is.False);
            Assert.That(_presenter.TryStartle(_state, _config, 1d, false, random), Is.False);
            Assert.That(_state.StartlesUsed, Is.Zero);
            Assert.That(_presenter.TryStartle(_state, _config, 1d, true, random), Is.False);
            Assert.That(_presenter.TryStartle(_state, _config, 0d, true, random), Is.False);
        }

        [Test]
        public void ExhaustedIntrusionProducesSubtlePostFXInsteadOfAnotherStartle()
        {
            var config = ScriptableObject.CreateInstance<Worsen.Presentation.PostFX.PostFXDriverConfig>();
            try
            {
                var state = new Worsen.Presentation.PostFX.PostFXDriverState();
                var post = new Worsen.Presentation.PostFX.PostFXPresenter();
                var random = new System.Random(22);
                for (int i = 0; i < 3; i++)
                {
                    bool startle = _presenter.TryStartle(_state, _config, i * 120d, true, random);
                    post.PlayIntrusion(state, 2f, startle);
                    post.Tick(state, config, 0f);
                    float strength = i < 2 ? 1f : config.SubtleIntrusionMultiplier;
                    Assert.That(state.Grain, Is.EqualTo(config.BaselineGrain + config.IntrusionGrain * strength));
                    post.Tick(state, config, 2f);
                }
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void ChanceUsesOnlyTheInjectedSeedAndNeverExceedsCount()
        {
            var serialized = new SerializedObject(_config);
            serialized.FindProperty("_earnedStartleChance").floatValue = 0.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var random = new System.Random(22);
            var oracle = new System.Random(22);
            int count = 0;
            for (int i = 0; i < 64; i++)
            {
                bool expected = count < 2 && oracle.NextDouble() < 0.5d;
                Assert.That(_presenter.TryStartle(_state, _config, i * 120d, true, random), Is.EqualTo(expected));
                if (expected) count++;
            }
            Assert.That(_state.StartlesUsed, Is.EqualTo(2));
        }

        [TestCase(false, false)] [TestCase(true, false)] [TestCase(false, true)] [TestCase(true, true)]
        public void FogHooksComposeAndCanBeTurnedOff(bool darker, bool eyes)
        {
            _presenter.SetLightingHooks(_state, _config, darker, eyes);
            _presenter.CalculateAtmosphere(_state, _settings, 100f);
            float distance = darker ? _config.DarkerFogDistanceMultiplier : 1f;
            Assert.That(_state.FogCurveStart, Is.EqualTo(.08f * distance * (eyes ? _config.CatEyesFogStartMultiplier : 1f)).Within(.00001f));
            Assert.That(_state.FogCurveEnd, Is.EqualTo(.24f * distance).Within(.00001f));
            _presenter.SetLightingHooks(_state, _config, false, false);
            _presenter.CalculateAtmosphere(_state, _settings, 100f);
            Assert.That(_state.FogCurveStart, Is.EqualTo(.08f).Within(.00001f));
            Assert.That(_state.FogCurveEnd, Is.EqualTo(.24f).Within(.00001f));
        }

        private HorrorAttackVisual Present(HorrorAttackDriverState state, int phase, float progress)
        {
            return _presenter.PresentAttack(state,
                new HunterAttackSample(new EntityId(1), Vector3.zero, Vector3.forward, phase, progress), _settings);
        }
    }
}

