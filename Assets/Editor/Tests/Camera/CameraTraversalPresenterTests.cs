// ============================================================================
// CameraTraversalPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Tests traversal composition against progress rather than an assumed vault timer.
//   Supplied movement verifies mouse look remains independent of all cosmetic offsets.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Camera.
// KEY RESPONSIBILITIES:
//   - Verify cancellation, severity, duration, comfort overrides and relay handlers.
//   - Verify render interpolation across progress, cancellation and completion boundaries.
// DEPENDENCIES:
//   - Core, Camera presentation, CameraOrchestrator, NUnit and reflection test injection.
// USAGE NOTES:
//   Edit Mode. Injects only owned presentation state; never starts a camera or changes a scene.
// ============================================================================
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Orchestrator;
using Worsen.Presentation.Camera;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Camera
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class CameraTraversalPresenterTests
    {
        private CameraDriverConfig _config;
        private CameraDriverState _state;
        private CameraFeedbackPresenter _presenter;
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<CameraDriverConfig>();
            _state = new CameraDriverState(); _presenter = new CameraFeedbackPresenter();
            Movement(1, 0f, 0f);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(_config);
        private void Movement(long tick, float yaw, float pitch) => _presenter.SetMovement(_state, _config,
            new PlayerMovementSample(new EntityId(1), tick, Vector3.zero, Vector3.zero, Vector3.up,
                yaw, new Vector2(0f, pitch), false, MovementState.Vault, 1f));
        private void Progress(long tick, float progress, bool active) => CameraTraversalPresenter.SetProgress(
            _state, _config, new EntityId(1), tick, TraversalKind.Vault, progress, active);
        private void Tick(float dt) => _presenter.Tick(_state, _config, dt, 1f);

        [Test]
        public void TimedProgressInterpolatesHeightAndCompletionDoesNotDropTheLastPair()
        {
            CameraTraversalPresenter.SetProgress(_state, _config, new EntityId(1), 1,
                TraversalKind.Vault, .65f, true, 1f, .02f);
            _presenter.Tick(_state, _config, 0f, 1f, 1.01f);
            Assert.That(_state.Position.y, Is.EqualTo(1.04f).Within(.00001f));
            CameraTraversalPresenter.SetProgress(_state, _config, new EntityId(1), 2,
                TraversalKind.Vault, 1f, false, 1.02f, .02f);
            _presenter.Tick(_state, _config, 0f, 1f, 1.02f);
            Assert.That(_state.Position.y, Is.EqualTo(1.08f).Within(.00001f));
            _presenter.Tick(_state, _config, 0f, 1f, 1.03f);
            Assert.That(_state.Position.y, Is.EqualTo(1.04f).Within(.00001f));
            _presenter.Tick(_state, _config, 0f, 1f, 1.05f);
            Assert.That(_state.Position.y, Is.EqualTo(1f).Within(.00001f));
            Assert.That(_state.VaultActive || _state.VaultCompleting, Is.False);
        }

        [Test]
        public void TimedCancellationRecoversFromDisplayedNotFutureHeight()
        {
            CameraTraversalPresenter.SetProgress(_state, _config, new EntityId(1), 1,
                TraversalKind.Vault, .65f, true, 1f, .02f);
            _presenter.Tick(_state, _config, 0f, 1f, 1.01f);
            float displayed = _state.Position.y;
            Progress(2, .7f, false);
            _presenter.Tick(_state, _config, 0f, 1f, 1.02f);
            Assert.That(_state.Position.y, Is.EqualTo(displayed).Within(.00001f));
            Tick(_config.VaultReturnSeconds);
            Assert.That(_state.Position.y, Is.EqualTo(1f).Within(.00001f));
        }

        [Test] public void VaultFollowsProgressNotTimeAndMouseLookStaysLive()
        {
            Progress(1, .2f, true); Tick(0f);
            Assert.That(_state.Position.y, Is.EqualTo(.96f).Within(.00001f));
            Tick(10f);
            Assert.That(_state.Position.y, Is.EqualTo(.96f).Within(.00001f));
            Movement(2, 65f, 12f); Progress(2, .65f, true); Tick(0f);
            Assert.That(_state.Position.y, Is.EqualTo(1.08f).Within(.00001f));
            Assert.That(Quaternion.Angle(_state.AimRotation, Quaternion.Euler(-12f, 65f, 0f)), Is.LessThan(.01f));
            Progress(1, 0f, false);
            Assert.That(_state.VaultActive, Is.True);
        }

        [Test] public void CancelPreservesCurrentOffsetThenEasesToZero()
        {
            Progress(1, .65f, true); Tick(0f); Progress(2, 0f, false); Tick(0f);
            Assert.That(_state.Position.y, Is.EqualTo(1.08f).Within(.00001f));
            Tick(_config.VaultReturnSeconds / 2f);
            Assert.That(_state.Position.y, Is.EqualTo(1.04f).Within(.00001f));
            Progress(3, 0f, false); Tick(_config.VaultReturnSeconds / 2f);
            Assert.That(_state.Position, Is.EqualTo(Vector3.up));
        }

        [TestCase(0f, .04f)] [TestCase(.5f, .08f)] [TestCase(1f, .12f)]
        public void LandingUsesNormalizedSeverity(float severity, float depth)
        {
            _presenter.PlayTraversal(_state, new PlayerTraversalFact(new EntityId(1), 1,
                TraversalKind.Land, true, Vector3.down, 99f), _config, severity);
            Tick(_config.LandingDipSeconds / 2f);
            Assert.That(_state.Position.y, Is.EqualTo(1f - depth).Within(.00001f));
            Tick(_config.LandingDipSeconds / 2f);
            Assert.That(_state.Position, Is.EqualTo(Vector3.up));
        }

        [Test] public void StumbleUsesFullSuppliedDurationWithoutChangingAim()
        {
            CameraTraversalPresenter.Stumble(_state, new EntityId(1), 2, 1.5f);
            Tick(1.02f);
            Assert.That(_state.Position.x, Is.Not.EqualTo(0f));
            Assert.That(_state.AimRotation, Is.EqualTo(Quaternion.identity));
            CameraTraversalPresenter.Stumble(_state, new EntityId(1), 2, 5f);
            Tick(.48f);
            Assert.That(_state.Position, Is.EqualTo(Vector3.up));
            Assert.That(_state.Rotation, Is.EqualTo(_state.AimRotation));
        }

        [TestCase(false, true)] [TestCase(true, false)]
        public void EitherRuntimeComfortSwitchSuppressesLandingAndStumble(bool punch, bool tilt)
        {
            CameraTraversalPresenter.Land(_state, _config, 1f);
            CameraTraversalPresenter.Stumble(_state, new EntityId(1), 1, 1f);
            Tick(.02f);
            _presenter.ApplySettings(_state, new PlayerSettingsRecord(1, 1f, false, 95f, tilt, punch, true, 1f, 1f, 1f));
            Tick(.02f);
            Assert.That(_state.Position, Is.EqualTo(Vector3.up));
            Assert.That(_state.Rotation, Is.EqualTo(_state.AimRotation));
            _presenter.ApplySettings(_state, new PlayerSettingsRecord(1, 1f, false, 95f, true, true, true, 1f, 1f, 1f)); Tick(.02f);
            Assert.That(_state.Position, Is.EqualTo(Vector3.up), "Suppressed envelopes must not resume.");
        }

        [Test] public void PublicPendingRelayHandlersReachOwnedDriverAndResetClearsThem()
        {
            var owner = new GameObject("Traversal handler test"); owner.SetActive(false);
            try
            {
                var driver = owner.AddComponent<CameraDriver>(); var manager = owner.AddComponent<CameraManager>();
                var route = owner.AddComponent<CameraOrchestrator>();
                Set(driver, "_state", _state); Set(driver, "_config", _config); Set(driver, "_presenter", _presenter);
                Set(manager, "_driver", driver); Set(manager, "_initialized", true);
                route.Configure(null, manager);
                route.OnTraversalProgressed(new EntityId(1), 1, TraversalKind.Mantle, .65f, true);
                route.OnPlayerStumbled(new EntityId(1), 2, .8f);
                Assert.That(_state.VaultHeight, Is.EqualTo(.08f).Within(.00001f));
                Assert.That(_state.StumbleDuration, Is.EqualTo(.8f));
                route.OnLanding(new PlayerTraversalFact(new EntityId(1), 3, TraversalKind.Land,
                    true, Vector3.down, .8f), 1f);
                Assert.That(_state.LandingDepth, Is.EqualTo(.12f));
                manager.ResetView();
                Assert.That(_state.VaultActive, Is.False);
                Assert.That(_state.StumbleDuration + _state.VaultHeight, Is.Zero);
            }
            finally { Object.DestroyImmediate(owner); }
        }
        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
