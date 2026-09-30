// ============================================================================
// CameraHandCatchPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies slow hand emergence, fast grab and the shared catch timing edges.
//   Explicit clocks and fog colors keep these tests independent of camera rendering.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Camera.
// KEY RESPONSIBILITIES:
//   - Protect configured phase order, fixed framing, one sting edge and one completion.
//   - Exercise zero/invalid time, hunter isolation, fog tint and reset/retrigger.
// DEPENDENCIES:
//   Camera presentation, Core movement facts, NUnit and transient configuration.
// USAGE NOTES:
//   Edit Mode pure math; no Unity rendering or audible-device acceptance is implied.
// ============================================================================
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
using Worsen.Presentation.Camera;
namespace Worsen.Tests.Camera
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class CameraHandCatchPresenterTests
    {
        private CameraDriverConfig _config;
        private CameraDriverState _state;
        private CameraFeedbackPresenter _presenter;
        [SetUp] public void Setup()
        {
            _config = ScriptableObject.CreateInstance<CameraDriverConfig>();
            _state = new CameraDriverState(); _presenter = new CameraFeedbackPresenter();
            _presenter.SetMovement(_state, _config, new PlayerMovementSample(new EntityId(7), 1,
                Vector3.zero, Vector3.zero, Vector3.up, 0f, Vector2.zero, false, MovementState.Ground, 0f));
            _presenter.Tick(_state, _config, 0f, 1f);
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(_config);
        private void Begin() => _presenter.PlayConsumed(_state, _config, Vector3.up + Vector3.forward * 3f);
        private void Tick(float dt) => _presenter.Tick(_state, _config, dt, 1f);

        [Test] public void SlowApproachThenFastGrabHoldsCameraAndCutsOnce()
        {
            Begin(); var position = _state.Position;
            Assert.That(_presenter.ConsumptionSeconds(_config), Is.EqualTo(1.35f).Within(.0001f));
            Assert.That(_state.HandReveal, Is.Zero);
            Tick(.6f);
            Assert.That(_state.HandReveal, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(_state.HandDistance, Is.EqualTo(1.325f).Within(.0001f));
            Assert.That(_state.CatchHoldStarted || _state.CatchHoldEnded, Is.False);
            int starts = 0, ends = 0;
            foreach (float dt in new[] { .6f, .075f, .075f, 10f })
            {
                bool started = _state.CatchHoldStarted, ended = _state.CatchHoldEnded;
                Tick(dt);
                if (!started && _state.CatchHoldStarted) starts++;
                if (!ended && _state.CatchHoldEnded) ends++;
                Assert.That(_state.Position, Is.EqualTo(position));
                Assert.That(_state.Rotation, Is.EqualTo(Quaternion.identity));
            }
            Assert.That(starts, Is.EqualTo(1)); Assert.That(ends, Is.EqualTo(1));
            Assert.That(_state.HandDistance, Is.EqualTo(_config.HandFaceDistance).Within(.0001f));
            Assert.That(_state.HandGrip, Is.EqualTo(1f));
            Begin(); Tick(1f); Assert.That(_state.CatchHoldElapsed, Is.EqualTo(.15f));
            _presenter.Reset(_state);
            Assert.That(_state.HandDistance + _state.HandReveal + _state.HandGrip, Is.Zero);
        }
        [Test] public void LargeDeltaDoesNotSwallowGrabAndIndependentConfigIsHonored()
        {
            var serialized = new SerializedObject(_config);
            serialized.FindProperty("_handApproachSeconds").floatValue = .4f;
            serialized.FindProperty("_handGrabSeconds").floatValue = .2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Begin(); Tick(10f);
            Assert.That(_state.CatchHoldStarted, Is.True);
            Assert.That(_state.CatchHoldEnded, Is.False);
            Assert.That(_state.HandDistance, Is.EqualTo(_config.HandReachDistance).Within(.0001f));
            Tick(.1f); Assert.That(_state.CatchHoldEnded, Is.False);
            Tick(.1f); Assert.That(_state.CatchHoldEnded, Is.True);
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)]
        public void InvalidDeltaNeverAdvancesHand(float dt)
        {
            Begin(); Tick(dt);
            Assert.That(_state.CatchElapsed + _state.CatchHoldElapsed + _state.HandReveal, Is.Zero);
            Assert.That(_state.HandDistance, Is.EqualTo(_config.HandStartDistance));
        }
        [Test] public void TintStartsExactlyInFogAndGripClosesTowardFace()
        {
            var fog = new Color(.01f, .02f, .03f);
            Assert.That(CameraHandCatchPresenter.Tint(fog, _config.HandColor, 0f), Is.EqualTo(fog));
            Assert.That(CameraHandCatchPresenter.Tint(fog, _config.HandColor, 1f), Is.EqualTo(_config.HandColor));
            Assert.That(CameraHandCatchPresenter.FingerRotation(0f, 90f), Is.EqualTo(Quaternion.identity));
            Assert.That(Vector3.Dot(CameraHandCatchPresenter.FingerRotation(1f, 90f) * Vector3.up, Vector3.back), Is.GreaterThan(.999f));
        }
        [Test] public void HunterCatchDoesNotUseHandConfigOrHandVisualOutputs()
        {
            _presenter.PlayDeathSnap(_state, _config, Vector3.forward * 3f);
            Tick(_config.CatchApproachSeconds);
            Assert.That(_state.CatchHoldStarted, Is.True);
            Assert.That(_state.Consumed, Is.False);
            Tick(_config.CatchHoldSeconds / 2f); Assert.That(_state.CatchHoldEnded, Is.False);
            Tick(_config.CatchHoldSeconds / 2f); Assert.That(_state.CatchHoldEnded, Is.True);
            Assert.That(_state.HandDistance + _state.HandReveal + _state.HandGrip, Is.Zero);
        }
    }
}
