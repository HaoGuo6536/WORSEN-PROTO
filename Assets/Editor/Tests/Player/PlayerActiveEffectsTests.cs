// ============================================================================
// PlayerActiveEffectsTests.cs
// ============================================================================
// PURPOSE:
//   Exercises effect delivery through real PlayerController ticks and actions.
//   These tests distinguish tuned arithmetic from rules actually reaching movement,
//   health, recovery, noises and read-only Floor grab protection.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Verify tick-boundary replacement, one-use momentum, action hooks and life reset.
// DEPENDENCIES:
//   - Player logic/config, Core effects/input, NUnit and temporary Unity configs.
// USAGE NOTES:
//   No physics queries or wall time. Mutable-view tests prove sampling happens at
//   Tick, not at SetActiveEffects or ApplyHit; external events must be replayed too.
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Player
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class PlayerActiveEffectsTests
    {
        private PlayerProfile _profile;
        private PlayerEffectConfig _config;
        private PlayerBehaviorState _state;
        private PlayerController _controller;
        private static readonly MovementProbe Ground = new MovementProbe(true, Vector3.up);
        private const float Dt = 1f / 60f;
        [SetUp] public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<PlayerProfile>();
            _config = ScriptableObject.CreateInstance<PlayerEffectConfig>();
            _state = new PlayerBehaviorState();
            _controller = new PlayerController(_state, _profile, new System.Random(77), _config);
            _controller.Reset(new EntityId(1), Vector3.zero, 0f);
        }
        [TearDown] public void TearDown()
        { Object.DestroyImmediate(_profile); Object.DestroyImmediate(_config); }
        private static InputFrame Frame(InputButtons pressed = InputButtons.None, InputButtons held = InputButtons.None, Vector2 move = default)
            => new InputFrame(move, Vector2.zero, held, pressed, InputButtons.None);
        private void Set(string id, int stacks = 1) => _controller.SetActiveEffects(PlayerEffectUtilityTests.Effects(id, stacks));
        private float Speed => new Vector2(_state.Velocity.x, _state.Velocity.z).magnitude;

        [Test]
        public void MutableViewAndReplacementApplyOnlyOnNextTickAndNeverCompoundHealth()
        {
            var view = new MutableView();
            _controller.SetActiveEffects(view);
            view.Value = PlayerEffectUtilityTests.Effects("thin-skin");
            Assert.That(_state.MaxHealth, Is.EqualTo(100f));
            _controller.Tick(default, Ground, Dt, 1);
            Assert.That(_state.MaxHealth, Is.EqualTo(75f));
            Assert.That(_state.Health, Is.EqualTo(75f));
            for (int i = 2; i < 5; i++) _controller.Tick(default, Ground, Dt, i);
            Assert.That(_state.MaxHealth, Is.EqualTo(75f));
            view.Value = default;
            Assert.That(_state.MaxHealth, Is.EqualTo(75f));
            _controller.SetHealthRecoveryEffects(0f);
            _controller.Tick(default, Ground, Dt, 5);
            Assert.That(_state.MaxHealth, Is.EqualTo(100f));
            Assert.That(_state.Health, Is.EqualTo(75f), "Removing Thin Skin does not grant health.");
            Set("thin-skin");
            _controller.Tick(default, Ground, Dt, 6);
            _controller.SetActiveEffects(null);
            Assert.That(_state.MaxHealth, Is.EqualTo(75f));
            _controller.Tick(default, Ground, Dt, 7);
            Assert.That(_state.MaxHealth, Is.EqualTo(100f));
        }

        [Test]
        public void HitRecoveryUsesCommittedSnapshotAndHeavyLegsCancelsExistingBoost()
        {
            Set("short-grace");
            _controller.ApplyHit(1f);
            Assert.That(_state.GraceWindow.EndTick, Is.EqualTo(72));
            _controller.EndRecovery();
            _controller.Tick(default, Ground, Dt, 1);
            _controller.ApplyHit(1f);
            Assert.That(_state.GraceWindow.EndTick, Is.EqualTo(45));
            Set("heavy-legs");
            Assert.That(_state.HitBoostMultiplier, Is.GreaterThan(1f));
            _controller.Tick(default, Ground, Dt, 2);
            Assert.That(_state.HitBoostMultiplier, Is.EqualTo(1f));
            Assert.That(_state.GraceWindow.EndTick, Is.EqualTo(45), "Already-published intervals stay stable.");
            _controller.EndRecovery();
            _controller.ApplyHit(1f);
            Assert.That(_state.HitBoostEndTick, Is.EqualTo(2));
        }

        [TestCase("slow-mend", 1, 50.75f)]
        [TestCase("no-regen", 1, 50f)]
        [TestCase("field-kit", 3, 53.75f)]
        public void RegenerationEffectsReachTheExistingHook(string id, int stacks, float expected)
        {
            Set(id, stacks);
            _controller.ApplyHit(50f);
            _controller.Tick(default, Ground, 5f, 300);
            Assert.That(_state.Health, Is.EqualTo(expected));
        }

        [Test]
        public void FloorStartUsesPendingRoughStartAndThinSkinAndPreservesInterveningDamage()
        {
            _controller.SetHealthRecoveryEffects(0f);
            _controller.SetActiveEffects(new ActiveEffects(new[] {
                new ActiveEffect(new EffectId("rough-start"), EffectKind.Curse, 1),
                new ActiveEffect(new EffectId("thin-skin"), EffectKind.Curse, 1) }));
            _controller.BeginFloorHealth(80f, 1f);
            _controller.ApplyHit(5f);
            _controller.Tick(default, Ground, Dt, 1);
            Assert.That(_state.MaxHealth, Is.EqualTo(60f));
            Assert.That(_state.Health, Is.EqualTo(25f));
            _controller.Tick(default, Ground, Dt, 2);
            Assert.That(_state.Health, Is.EqualTo(25f), "Rough Start is not a per-tick health reset.");
            _controller.EndRecovery();
            _controller.BeginFloorHealth(80f, 1f);
            _controller.ApplyHit(100f);
            _controller.SetActiveEffects(null);
            _controller.Tick(default, Ground, Dt, 3);
            Assert.That(_state.Health, Is.Zero, "A pending floor reconciliation never revives a death.");
        }

        [Test]
        public void QuickStartOnlyChangesStandstillAccelerationAndAirControlChangesSteering()
        {
            Set("quick-start", 3);
            _controller.Tick(Frame(move: Vector2.up), Ground, Dt, 1);
            Assert.That(Speed, Is.EqualTo(96f * Dt).Within(0.00001f));
            _controller.Tick(Frame(move: Vector2.up), Ground, Dt, 2);
            Assert.That(Speed, Is.EqualTo((96f + 60f) * Dt).Within(0.00001f));
            Set("air-control", 3);
            _state.MovementState = MovementState.Air;
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(move: Vector2.right), default, Dt, 3);
            Vector3 expected = Vector3.ClampMagnitude(Vector3.forward * 8f + Vector3.right * 43.75f * Dt, 8f);
            Assert.That(_state.Velocity.x, Is.EqualTo(expected.x).Within(0.00001f));
        }

        [Test]
        public void SprintCeilingStillHoldsWithRunAndHitBoostsAndSlideMomentum()
        {
            Set("speed-boost", 99);
            _controller.ApplyRunModifiers(100f, 100f, 2f);
            _controller.Tick(default, Ground, Dt, 0);
            _controller.ApplyHit(1f);
            for (int i = 1; i < 30; i++) _controller.Tick(Frame(held: InputButtons.Sprint, move: Vector2.up), Ground, Dt, i);
            Assert.That(Speed, Is.EqualTo(9.49f).Within(0.00001f));
            _controller.Tick(Frame(InputButtons.Crouch), Ground, Dt, 30);
            Assert.That(Speed, Is.LessThan(_config.HunterChaseSpeedCeiling));
            Assert.That(_state.SprintSpeed, Is.EqualTo(_profile.SprintSpeed), "Hunter reference remains nominal.");
        }

        [TestCase(false, 1f)] [TestCase(false, 1.5f)] [TestCase(true, 1f)]
        public void FastHandsReachesVaultMantleAndAutomaticLedge(bool ledge, float height)
        {
            Set("fast-hands", 3);
            _state.MovementState = MovementState.Air;
            var probe = new MovementProbe(false, Vector3.up, vaultCandidate: !ledge, vaultHeight: height,
                vaultClearance: 1.8f, vaultTarget: new Vector3(0f, height, 1f));
            Assert.That(_controller.Tick(Frame(ledge ? InputButtons.None : InputButtons.Jump), probe, Dt, 1).Traversing, Is.True);
            float baseline = ledge || height > _profile.VaultMaximumHeight ? _profile.MantleDuration : _profile.VaultDuration;
            Assert.That(_state.VaultDuration, Is.EqualTo(baseline * Mathf.Pow(0.85f, 3)).Within(0.00001f));
            Assert.That(_state.VaultDuration, Is.GreaterThanOrEqualTo(0.1f));
        }

        [Test]
        public void HigherJumpScalesHeightNotVelocityAndLongerSlideKeepsMoreSpeed()
        {
            Set("higher-jump");
            _controller.Tick(Frame(InputButtons.Jump), Ground, Dt, 1);
            Assert.That(_state.Velocity.y + _profile.Gravity * Dt, Is.EqualTo(_profile.JumpSpeed * Mathf.Sqrt(1.2f)).Within(0.00001f));
            _controller.Reset(new EntityId(1), Vector3.zero, 0f);
            Set("longer-slide");
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(InputButtons.Crouch), Ground, 0.1f, 1);
            Assert.That(_state.SlideRemaining, Is.EqualTo(1.46f).Within(0.00001f));
            Assert.That(Speed, Is.GreaterThan(Mathf.Lerp(8f, 10f, 1.1f / 1.2f)));
            Assert.That(_controller.SlideWallSpeedRetention, Is.EqualTo(0.95f).Within(0.00001f));
        }

        [Test]
        public void QuietSlideEmitsNothingLowProfileOnlyProtectsSlidesAndNoLookBackDisablesSnap()
        {
            _controller.SetActiveEffects(new ActiveEffects(new[] {
                new ActiveEffect(new EffectId("quiet-slide"), EffectKind.Upgrade, 1),
                new ActiveEffect(new EffectId("low-profile"), EffectKind.Upgrade, 1),
                new ActiveEffect(new EffectId("no-look-back"), EffectKind.Curse, 1) }));
            _state.Velocity = Vector3.forward * 8f;
            Assert.That(_state.IsUngrabbable, Is.False);
            _controller.Tick(Frame(InputButtons.Crouch, InputButtons.LookBack), Ground, Dt, 1);
            Assert.That(_state.RecentNoises, Is.Empty);
            Assert.That(((IReadOnlyPlayerEffectState)_state).IsUngrabbable, Is.True);
            Assert.That(_state.LookBack, Is.False);
            _controller.SetActiveEffects(null);
            Assert.That(_state.IsUngrabbable, Is.True);
            _controller.Tick(Frame(held: InputButtons.LookBack), Ground, Dt, 2);
            Assert.That(_state.IsUngrabbable, Is.False);
            Assert.That(_state.LookBack, Is.True);
            Set("low-profile");
            _controller.Tick(Frame(InputButtons.Jump), Ground, Dt, 3);
            Assert.That(_state.IsUngrabbable, Is.False);
        }

        [Test]
        public void SoftLandingSuppressesOnlyHardLandingStumbleNotImpactRetentionOrNoise()
        {
            Set("soft-landing");
            _state.MovementState = MovementState.Air;
            _state.Velocity = new Vector3(0f, -20f, 10f);
            _controller.Tick(default, Ground, Dt, 1);
            Assert.That(_state.StumbleRemaining, Is.Zero);
            Assert.That(_state.StumbleStartedSeconds, Is.Zero);
            Assert.That(Speed, Is.EqualTo(3f));
            Assert.That(_state.RecentNoises[0].SourceKind, Is.EqualTo(NoiseSourceKind.Landing));
        }

        [TestCase(false)] [TestCase(true)]
        public void VaultMomentumReleasesOnceOrExpires(bool expire)
        {
            Set("stored-momentum");
            _state.Velocity = Vector3.forward * 8f;
            var probe = new MovementProbe(true, Vector3.up, vaultCandidate: true, vaultHeight: 1f,
                vaultClearance: 1.8f, vaultTarget: Vector3.forward);
            _controller.Tick(Frame(InputButtons.Jump), probe, 0.25f, 1);
            Assert.That(_state.StoredMomentumSpeed, Is.EqualTo(8f));
            _controller.CommitPose(new PlayerMoveResult(Vector3.forward, Vector3.forward * 8f, true, false));
            _state.MovementState = MovementState.Ground;
            _state.Velocity = Vector3.forward * 2f;
            _controller.Tick(Frame(InputButtons.Jump), Ground, expire ? 1.01f : Dt, 2);
            Assert.That(Speed, Is.EqualTo(expire ? 2f : 8f).Within(0.00001f));
            Assert.That(_state.StoredMomentumSpeed, Is.Zero);
            _state.MovementState = MovementState.Ground;
            _state.Velocity = Vector3.forward * 2f;
            _controller.Tick(Frame(InputButtons.Jump), Ground, Dt, 3);
            Assert.That(Speed, Is.EqualTo(2f));
            _controller.Reset(new EntityId(2), Vector3.zero, 0f);
            Assert.That(_state.ActiveEffects, Is.Null);
            Assert.That(_state.AppliedEffects.Count, Is.Zero);
            Assert.That(_state.StoredMomentumRemaining, Is.Zero);
            Assert.That(_state.IsUngrabbable, Is.False);
        }

        private sealed class MutableView : IReadOnlyActiveEffects
        {
            public ActiveEffects Value;
            public int Count => Value.Count;
            public bool Has(EffectId id) => Value.Has(id);
            public int Stacks(EffectId id) => Value.Stacks(id);
            public IEnumerator<ActiveEffect> GetEnumerator() => Value.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
