// ============================================================================
// HunterAnimationPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies Hunter behavior with explicit reproducible fixtures.
//   Tests exercise observable light, physical attacks, route admission or creature
//   animation contracts without changing authored gameplay assets.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10), test suite (section 11) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Keep per-life state separate from shared configuration and foreign systems.
//   - Cover injected pose cadence, catch priority, rig fallback and independent foot weights.
//   - Invoke the no-rig IK callback directly, without Edit Mode native message dispatch.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Coordinator runs Unity tests with the exclusive lease. Fixtures clean up their own objects.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Hunter;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HunterAnimationPresenterTests
    {
        [Test] public void StopMotionAccumulatesInjectedTimeAndBoundsHitchCatchup()
        {
            var presenter = new HunterAnimationPresenter(); var state = new HunterAnimationDriverState();
            for (int i = 0; i < 4; i++) Assert.That(presenter.PoseSteps(state, 0.02f, 10f, 8, out _), Is.Zero);
            Assert.That(presenter.PoseSteps(state, 0.02f, 10f, 8, out float step), Is.EqualTo(1));
            Assert.That(step, Is.EqualTo(0.1f).Within(0.000001f));
            Assert.That(presenter.PoseSteps(state, 0.25f, 10f, 8, out _), Is.EqualTo(2));
            Assert.That(presenter.PoseSteps(state, 0.05f, 10f, 8, out _), Is.EqualTo(1));
            Assert.That(presenter.PoseSteps(state, 10f, 10f, 8, out _), Is.EqualTo(8));
            Assert.That(presenter.PoseSteps(state, 0.01f, 10f, 8, out _), Is.Zero);
            Assert.That(presenter.PoseSteps(state, 0.017f, 0f, 8, out step), Is.EqualTo(1));
            Assert.That(step, Is.EqualTo(0.017f));
            Assert.That(state.PoseRemainder, Is.Zero);
            Assert.That(presenter.PoseSteps(state, float.NaN, 10f, 8, out _), Is.Zero);
        }
        [Test] public void LookBlendsButCatchImmediatelyPinsWeightUntilExplicitEnd()
        {
            var presenter = new HunterAnimationPresenter();
            var state = new HunterAnimationDriverState { HasHumanoidRig = true, Looking = true, LookTarget = Vector3.one };
            presenter.Look(state, 0.1f, 0.25f, 0.2f);
            Assert.That(state.LookWeight, Is.EqualTo(0.4f).Within(0.000001f));
            Assert.That(state.LookTarget, Is.EqualTo(Vector3.one));
            state.LookTarget = Vector3.forward * 4; state.CatchActive = true; state.Looking = false;
            presenter.Look(state, 0f, 0.25f, 0.2f);
            Assert.That(state.LookWeight, Is.EqualTo(1f));
            presenter.Look(state, 20f, 0.25f, 0.2f);
            Assert.That(state.LookWeight, Is.EqualTo(1f));
            Assert.That(state.LookTarget, Is.EqualTo(Vector3.forward * 4));
            state.CatchActive = false; presenter.Look(state, 0.1f, 0.25f, 0.2f);
            Assert.That(state.LookWeight, Is.EqualTo(0.5f).Within(0.000001f));
        }
        [Test] public void MissingRigZerosAllIKAndFeetBlendIndependentlyOnSplitLevels()
        {
            var presenter = new HunterAnimationPresenter();
            var state = new HunterAnimationDriverState { Looking = true, CatchActive = true, LookWeight = 1f };
            presenter.Look(state, 0.1f, 0.25f, 0.2f);
            presenter.Foot(state, 0, true, Vector3.up, Quaternion.identity, 0.1f, 0.2f);
            Assert.That(state.LookWeight, Is.Zero); Assert.That(state.FootWeights[0], Is.Zero);
            state.HasHumanoidRig = true;
            presenter.Foot(state, 0, true, Vector3.up, Quaternion.identity, 0.1f, 0.2f);
            presenter.Foot(state, 1, true, Vector3.zero, Quaternion.Euler(15, 0, 0), 0.1f, 0.2f);
            Assert.That(state.FootPositions[0].y, Is.EqualTo(1f)); Assert.That(state.FootPositions[1].y, Is.Zero);
            Assert.That(state.FootWeights, Is.EqualTo(new[] { 0.5f, 0.5f }));
            presenter.Foot(state, 0, false, Vector3.zero, Quaternion.identity, 0.1f, 0.2f);
            Assert.That(state.FootWeights, Is.EqualTo(new[] { 0f, 0.5f }));
        }
        [Test] public void FootTargetsFollowStairsAndRampsButRejectWallsAndUnreachableLevels()
        {
            var presenter = new HunterAnimationPresenter();
            Assert.That(presenter.FootTarget(Vector3.zero, Quaternion.identity, Vector3.up * 0.2f, Vector3.up,
                0.03f, 0.5f, 60f, out Vector3 stair, out _), Is.True);
            Assert.That(stair.y, Is.EqualTo(0.23f).Within(0.000001f));
            Vector3 normal = Quaternion.Euler(30, 0, 0) * Vector3.up;
            Assert.That(presenter.FootTarget(Vector3.zero, Quaternion.identity, Vector3.zero, normal,
                0.03f, 0.5f, 60f, out _, out Quaternion ramp), Is.True);
            Assert.That(Vector3.Angle(ramp * Vector3.up, normal), Is.LessThan(0.01f));
            Assert.That(presenter.FootTarget(Vector3.zero, Quaternion.identity, Vector3.zero, Vector3.right,
                0.03f, 0.5f, 60f, out _, out _), Is.False);
            Assert.That(presenter.FootTarget(Vector3.zero, Quaternion.identity, Vector3.up, Vector3.up,
                0.03f, 0.5f, 60f, out _, out _), Is.False);
        }
        [Test] public void AnimatorBackendWithoutAHumanoidRigNoOpsSafely()
        {
            var root = new GameObject("No humanoid fixture");
            var config = ScriptableObject.CreateInstance<HunterAnimationDriverConfig>();
            try
            {
                var backend = root.AddComponent<HunterAnimatorIKDriver>();
                var state = new HunterAnimationDriverState();
                backend.Bind(root.AddComponent<Animator>(), root.transform, config, state);
                var callback = typeof(HunterAnimatorIKDriver).GetMethod("OnAnimatorIK",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(callback, Is.Not.Null);
                Assert.DoesNotThrow(() => callback.Invoke(backend, new object[] { 0 }));
                Assert.That(state.IKApplied, Is.False);
                backend.Unbind();
                Assert.DoesNotThrow(() => callback.Invoke(backend, new object[] { 0 }));
                Assert.That(state.IKApplied, Is.False);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(config); }
        }
        [TestCase(0f, 0, 0)] [TestCase(2f, 0, 1)] [TestCase(8f, 0, 2)]
        [TestCase(0f, 1, 3)] [TestCase(18f, 2, 4)] [TestCase(0f, 3, 5)]
        public void SelectsCorrectRole(float speed, int phase, int expected)
        { Assert.That(new HunterAnimationPresenter().Tick(new HunterAnimationDriverState(), 0.02f, speed, phase, 4f, 0.1f), Is.EqualTo(expected)); }
        [Test] public void RepeatedTransitionsKeepWeightsNormalizedAndSettle()
        {
            var presenter = new HunterAnimationPresenter(); var state = new HunterAnimationDriverState();
            presenter.Tick(state, 0.1f, 8f, 0, 4f, 0.1f);
            for (int i = 0; i < 20; i++)
            {
                presenter.Tick(state, 0.02f, 0f, 1, 4f, 0.1f);
                float total = 0; foreach (float weight in state.Weights) total += weight;
                Assert.That(total, Is.EqualTo(1f).Within(0.0001f));
            }
            Assert.That(state.Weights[3], Is.EqualTo(1f).Within(0.0001f));
        }
    }
}
