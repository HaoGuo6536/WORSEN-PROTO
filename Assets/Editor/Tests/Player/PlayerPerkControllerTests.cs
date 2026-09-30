// ============================================================================
// PlayerPerkControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies chase/contact perk rules without creating Unity engine objects.
//   Uninitialized config objects receive explicit fixture tuning through reflection.
//   This makes the rule tests runnable in a standalone managed process as well.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · Player.
// KEY RESPONSIBILITIES:
//   - Verify one body rebound per chase, invalid contacts and stable chase identity.
//   - Verify delayed footstep origins, bounded history and exact effect keys.
//   - Verify chase-only sprint/heartbeat, glancing protection and per-room latching.
//   - Verify floor/life reset clears all transient perk state.
// DEPENDENCIES:
//   - Player pure state/controller/config, Core immutable values and NUnit.
// USAGE NOTES:
//   No engine calls, native ScriptableObject construction, wall time or test skips.
//   Fixture tuning is supplied explicitly; defaults are also checked in the
//   PlayerActiveEffectsTests Unity fixture, so this does not replace asset tests.
// ============================================================================
using System;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Player
{
    public sealed class PlayerPerkControllerTests
    {
        private PlayerBehaviorState state;
        private PlayerEffectConfig config;
        private PlayerPerkController perks;
        private readonly EntityId hunter = new EntityId(-1);
        [SetUp] public void SetUp()
        {
            config = (PlayerEffectConfig)FormatterServices.GetUninitializedObject(typeof(PlayerEffectConfig));
            Field("_secondBounceId", "second-bounce"); Field("_echoBootsId", "echo-boots");
            Field("_loudHeartId", "loud-heart"); Field("_sureFootingId", "sure-footing"); Field("_latchId", "latch");
            Field("_echoBootsDelaySeconds", 3f); Field("_loudHeartSprintMultiplier", 1.1f);
            Field("_heartbeatIntervalSeconds", 1f); Field("_heartbeatLoudness", .5f);
            Field("_hunterChaseSpeedCeiling", 9.5f); Field("_chaseSpeedMargin", .01f);
            state = new PlayerBehaviorState { Id = new EntityId(1), Health = 100f, MaxHealth = 100f,
                RecoveryTickSeconds = 1f / 60f, MovementState = MovementState.Air,
                ReboundJumpRemaining = .1f, Velocity = Vector3.forward * 8f };
            perks = new PlayerPerkController(state, config);
        }
        private void Field(string name, object value)
            => typeof(PlayerEffectConfig).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, value);
        private void Effect(string id, int stacks = 1)
            => state.AppliedEffects = new ActiveEffects(new[] { new ActiveEffect(new EffectId(id), EffectKind.Upgrade, stacks) });
        private void Chase(int id, long tick = 0, ChasePhase phase = ChasePhase.Confirmed)
            => perks.ReceiveChase(new ChaseFact(id, state.Id, hunter, tick, phase));
        private bool Bounce(Vector3 normal) => perks.TryBodyRebound(hunter, normal, 3f, 14f, out _);

        private PlayerController MotionController()
        {
            var profile = (PlayerProfile)FormatterServices.GetUninitializedObject(typeof(PlayerProfile));
            void Tune(string name, float value) => typeof(PlayerProfile).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(profile, value);
            Tune("_maximumHealth", 100f); Tune("_sprintSpeed", 8f); Tune("_maxDesignSpeed", 14f);
            Tune("_maximumExternalMotionSpeed", 14f); Tune("_reboundUpwardBoost", 3f);
            Tune("_injuredThreshold", 50f); Tune("_criticalThreshold", 25f); Tune("_injuredSpeedMultiplier", .95f);
            Tune("_hitGraceSeconds", 1.2f); Tune("_heavyHitBoostSeconds", 1.2f); Tune("_heavyHitSpeedBoost", .25f);
            Tune("_traversalLoudness", 1f);
            state.NoiseRing = new NoiseEvent[16]; state.BaseMaximumHealth = 100f;
            state.SprintSpeed = 8f; state.MaxDesignSpeed = 14f;
            return new PlayerController(state, profile, new System.Random(1), config);
        }

        [Test]
        public void SecondBounceReflectsOncePerChaseAndDoesNotRearmOnLostOrDuplicateStart()
        {
            Effect("second-bounce", 99); Chase(1);
            Assert.That(perks.TryBodyRebound(hunter, Vector3.back, 3f, 7f, out Vector3 velocity), Is.True);
            Assert.That(velocity.z, Is.EqualTo(-7f)); Assert.That(velocity.y, Is.EqualTo(3f));
            Assert.That(Bounce(Vector3.back), Is.False);
            Chase(1, 1, ChasePhase.Lost); Chase(1, 2);
            Assert.That(Bounce(Vector3.back), Is.False);
            Chase(1, 3, ChasePhase.None); Chase(1, 4);
            Assert.That(Bounce(Vector3.back), Is.False, "An ended chase cannot be resurrected by a delayed start.");
            Chase(2, 5); Assert.That(Bounce(Vector3.back), Is.True);
        }

        [Test]
        public void RejectedBodyContactsDoNotSpendAllowance()
        {
            Effect("second-bounce");
            Assert.That(Bounce(Vector3.back), Is.False); Chase(1);
            Assert.That(Bounce(Vector3.forward), Is.False); Assert.That(Bounce(Vector3.zero), Is.False);
            Assert.That(Bounce(new Vector3(float.NaN, 0f, 0f)), Is.False);
            Assert.That(perks.TryBodyRebound(EntityId.None, Vector3.back, 3f, 14f, out _), Is.False);
            Assert.That(perks.TryBodyRebound(state.Id, Vector3.back, 3f, 14f, out _), Is.False);
            state.ReboundJumpRemaining = 0f; Assert.That(Bounce(Vector3.back), Is.False);
            state.ReboundJumpRemaining = .1f; state.MovementState = MovementState.Ground;
            Assert.That(Bounce(Vector3.back), Is.False); state.MovementState = MovementState.Air;
            Assert.That(Bounce(Vector3.back), Is.True);
        }

        [TestCase("grace")] [TestCase("collision")] [TestCase("immunity")]
        public void ProtectedContactsNeverSpendTheBodyRebound(string protection)
        {
            Effect("second-bounce"); Chase(1);
            if (protection == "grace") state.GraceActive = true;
            else if (protection == "collision") typeof(PlayerBehaviorState).GetField("RevivalCollisionEndTick",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(state, 10L);
            else typeof(PlayerBehaviorState).GetField("RevivalImmunityWindow",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(state, new GraceWindowFact(state.Id, 0, 10, HitSeverity.Light));
            Assert.That(Bounce(Vector3.back), Is.False);
            state.GraceActive = false; state.Tick = 10;
            Assert.That(Bounce(Vector3.back), Is.True);
        }

        [Test]
        public void PlayerBodyHookConsumesJumpPublishesNoiseAndPreservesWallRules()
        {
            var motion = MotionController(); Effect("second-bounce"); Chase(1);
            state.JumpBufferRemaining = .1f; state.LastReboundWall = 17; state.ReboundCooldownRemaining = .4f;
            Assert.That(motion.TryReboundFromHunter(hunter, Vector3.back, out var fact), Is.True);
            Assert.That(state.Velocity, Is.EqualTo(new Vector3(0f, 3f, -8f)));
            Assert.That(state.JumpBufferRemaining, Is.Zero); Assert.That(state.ReboundJumpRemaining, Is.Zero);
            Assert.That(state.LastReboundWall, Is.EqualTo(17)); Assert.That(state.ReboundCooldownRemaining, Is.EqualTo(.4f));
            Assert.That(fact.Kind, Is.EqualTo(TraversalKind.Rebound)); Assert.That(fact.Succeeded, Is.True);
            Assert.That(state.RecentNoises.Count, Is.EqualTo(1));
            Assert.That(state.RecentNoises[0].SourceKind, Is.EqualTo(NoiseSourceKind.Rebound));
            Assert.That(state.RecentNoises[0].Position, Is.EqualTo(state.Position));
        }

        [Test]
        public void PlayerGlancingRamHookPreservesHealthShieldAndExactKnockbackButNormalHitsStillDamage()
        {
            var motion = MotionController(); Effect("sure-footing"); state.Shield = 10f;
            Assert.That(motion.TryDeflectGlancingRam(false, Vector3.right * 3f), Is.False);
            Assert.That(motion.TryDeflectGlancingRam(true, Vector3.right * 3f), Is.True);
            Assert.That(state.PendingExternalVelocity, Is.EqualTo(Vector3.right * 3f));
            Assert.That(state.Health, Is.EqualTo(100f)); Assert.That(state.Shield, Is.EqualTo(10f));
            Assert.That(state.GraceActive, Is.False); Assert.That(state.HitBoostMultiplier, Is.EqualTo(1f));
            Assert.That(motion.ApplyHit(30f).Changed, Is.True);
            Assert.That(state.Health, Is.EqualTo(80f)); Assert.That(state.Shield, Is.Zero);
            Assert.That(motion.TryDeflectGlancingRam(true, Vector3.right * 3f), Is.False);
        }

        [Test]
        public void PlayerSprintLimitUsesChaseOnlyMultiplierAndKeepsNominalHunterReference()
        {
            var motion = MotionController(); Effect("loud-heart");
            float Sprint() => (float)typeof(PlayerController).GetMethod("EffectiveSprintSpeed",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(motion, null);
            Assert.That(Sprint(), Is.EqualTo(8f)); Chase(1);
            Assert.That(Sprint(), Is.EqualTo(8.8f).Within(.00001f));
            state.MovementSpeedMultiplier = 2f;
            Assert.That(Sprint(), Is.EqualTo(9.49f).Within(.00001f));
            Assert.That(state.SprintSpeed, Is.EqualTo(8f));
            Chase(1, 1, ChasePhase.None); Assert.That(Sprint(), Is.EqualTo(16f));
        }

        [Test]
        public void FloorHealthResetClearsPerksAndRequiresNewCommittedSprint()
        {
            var motion = MotionController(); Effect("loud-heart"); Chase(1); perks.Tick();
            Effect("latch"); state.IsSprinting = true; Assert.That(motion.TryLatchDoor(0, 1), Is.True);
            motion.BeginFloorHealth(100f, 1f);
            Assert.That(motion.TakeHeartbeat(out _), Is.False); Assert.That(motion.TryLatchDoor(0, 1), Is.False);
            state.IsSprinting = true; Assert.That(motion.TryLatchDoor(0, 1), Is.True);
            Effect("loud-heart"); Assert.That(perks.LoudHeartActive, Is.False);
        }

        [Test]
        public void ChaseRejectsForeignStaleAndOldEndFacts()
        {
            Effect("loud-heart");
            perks.ReceiveChase(new ChaseFact(1, new EntityId(2), hunter, 0, ChasePhase.Confirmed));
            Assert.That(perks.LoudHeartActive, Is.False);
            Chase(2, 10); Chase(1, 11, ChasePhase.None); Chase(2, 9, ChasePhase.None);
            Assert.That(perks.LoudHeartActive, Is.True);
            Chase(2, 12, ChasePhase.Lost); Assert.That(perks.LoudHeartActive, Is.True);
            Chase(2, 13, ChasePhase.None); Assert.That(perks.LoudHeartActive, Is.False);
            Chase(2, 14); Assert.That(perks.LoudHeartActive, Is.False);
        }

        [TestCase(50)] [TestCase(60)] [TestCase(120)]
        public void EchoBootsUseThreeSecondOldPoseAndBoundTheTrail(int rate)
        {
            state.RecoveryTickSeconds = 1f / rate;
            Effect("echo-boots");
            for (int tick = 0; tick <= 10 * rate; tick++)
            {
                state.Tick = tick; state.Position = Vector3.right * tick; perks.Tick();
                Assert.That(perks.FootstepOrigin(), Is.EqualTo(Vector3.right * Math.Max(0, tick - 3 * rate)));
            }
            object perkState = typeof(PlayerBehaviorState).GetProperty("Perks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state);
            var positions = (System.Collections.ICollection)perkState.GetType().GetField("Positions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(perkState);
            Assert.That(positions.Count, Is.EqualTo(3 * rate + 1));
        }

        [Test]
        public void EchoHistoryRecordsEvenBeforeAcquisitionAndUsesOldestAvailableAcrossSparseTicks()
        {
            state.Position = Vector3.left; perks.Tick();
            state.Tick = 120; state.Position = Vector3.up; perks.Tick();
            Effect("echo-boots");
            state.Tick = 200; state.Position = Vector3.right; perks.Tick();
            Assert.That(perks.FootstepOrigin(), Is.EqualTo(Vector3.left));
            state.Tick = 300; perks.Tick(); Assert.That(perks.FootstepOrigin(), Is.EqualTo(Vector3.up));
            state.AppliedEffects = default; Assert.That(perks.FootstepOrigin(), Is.EqualTo(state.Position));
        }

        [Test]
        public void EchoHistoryRewindStartsANewTrailWithoutFuturePositions()
        {
            Effect("echo-boots"); state.Tick = 500; state.Position = Vector3.one; perks.Tick();
            state.Tick = 0; state.Position = Vector3.zero; perks.Tick();
            Assert.That(perks.FootstepOrigin(), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void LoudHeartOnlyRunsDuringChaseAndDeliversEachHeartbeatOnceWithoutMovingItsOrigin()
        {
            Effect("loud-heart", 99); Assert.That(perks.SprintMultiplier, Is.EqualTo(1f));
            perks.Tick(); Assert.That(perks.TakeHeartbeat(out _), Is.False);
            Chase(1); state.Position = Vector3.one; perks.Tick();
            Assert.That(perks.SprintMultiplier, Is.EqualTo(1.1f));
            Assert.That(perks.TakeHeartbeat(out var noise), Is.True);
            Assert.That(noise.Source, Is.EqualTo(state.Id)); Assert.That(noise.Position, Is.EqualTo(Vector3.one));
            Assert.That(noise.Loudness, Is.EqualTo(.5f)); Assert.That(noise.Tick, Is.Zero);
            Assert.That(perks.TakeHeartbeat(out _), Is.False);
            state.Tick = 59; perks.Tick(); Assert.That(perks.TakeHeartbeat(out _), Is.False);
            state.Tick = 60; perks.Tick(); Assert.That(perks.TakeHeartbeat(out _), Is.True);
            Chase(1, 61, ChasePhase.None); state.Tick = 61; perks.Tick();
            Assert.That(perks.TakeHeartbeat(out _), Is.False); Assert.That(perks.SprintMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void LoudHeartRemovalAndDeathStopHeartbeatAndLargeTicksDoNotBurst()
        {
            Effect("loud-heart"); Chase(1); state.Tick = 600; perks.Tick();
            Assert.That(perks.TakeHeartbeat(out _), Is.True); Assert.That(perks.TakeHeartbeat(out _), Is.False);
            state.AppliedEffects = default; state.Tick = 660; perks.Tick();
            Assert.That(perks.TakeHeartbeat(out _), Is.False);
            Effect("loud-heart"); state.Health = 0f; perks.Tick();
            Assert.That(perks.TakeHeartbeat(out _), Is.False); Assert.That(perks.SprintMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void SureFootingRequiresTheExactEffectAndALivingPlayer()
        {
            Assert.That(perks.ProtectsGlancingRam, Is.False); Effect("sure-footing");
            Assert.That(perks.ProtectsGlancingRam, Is.True); state.Health = 0f;
            Assert.That(perks.ProtectsGlancingRam, Is.False);
        }

        [Test]
        public void LatchOnlyConsumesTheFirstActualSprintDoorInEachRoom()
        {
            Effect("latch"); Assert.That(perks.TryLatchDoor(0, 1), Is.False);
            state.IsSprinting = true;
            Assert.That(perks.TryLatchDoor(-1, 1), Is.False); Assert.That(perks.TryLatchDoor(0, 0), Is.False);
            Assert.That(perks.TryLatchDoor(0, 1), Is.True); Assert.That(perks.TryLatchDoor(0, 2), Is.False);
            Assert.That(perks.TryLatchDoor(1, 2), Is.True); Assert.That(perks.TryLatchDoor(0, 1), Is.False);
            state.Health = 0f; Assert.That(perks.TryLatchDoor(2, 3), Is.False);
        }

        [Test]
        public void ResetClearsChaseContactDoorsHeartbeatAndHistory()
        {
            Effect("loud-heart"); Chase(1); state.Position = Vector3.one; perks.Tick();
            Effect("second-bounce"); Assert.That(Bounce(Vector3.back), Is.True);
            Effect("latch"); state.IsSprinting = true; Assert.That(perks.TryLatchDoor(0, 1), Is.True);
            perks.Reset(); Assert.That(perks.TakeHeartbeat(out _), Is.False);
            Effect("echo-boots"); state.Position = Vector3.zero; Assert.That(perks.FootstepOrigin(), Is.EqualTo(Vector3.zero));
            Effect("second-bounce"); Chase(1); Assert.That(Bounce(Vector3.back), Is.True);
            Effect("latch"); Assert.That(perks.TryLatchDoor(0, 1), Is.True);
        }

        [Test]
        public void PerkKeysAreExactDesignerDataNotHardcodedInRules()
        {
            Field("_loudHeartId", "Custom Exact Key"); Effect("loud-heart"); Chase(1);
            Assert.That(perks.LoudHeartActive, Is.False); Effect("custom exact key");
            Assert.That(perks.LoudHeartActive, Is.False); Effect("Custom Exact Key");
            Assert.That(perks.LoudHeartActive, Is.True);
        }

        [Test]
        public void NullConfigIsNeutralAndCannotAdmitContactPerks()
        {
            perks = new PlayerPerkController(state, null); Effect("second-bounce"); Chase(1); perks.Tick();
            Assert.That(Bounce(Vector3.back), Is.False); Assert.That(perks.TakeHeartbeat(out _), Is.False);
            Assert.That(perks.FootstepOrigin(), Is.EqualTo(state.Position));
            Assert.That(perks.SprintMultiplier, Is.EqualTo(1f)); Assert.That(perks.ProtectsGlancingRam, Is.False);
            state.IsSprinting = true; Assert.That(perks.TryLatchDoor(0, 1), Is.False);
        }

        [TestCase(float.NaN)] [TestCase(-1f)] [TestCase(float.PositiveInfinity)]
        public void InvalidEchoDelayFailsExplicitly(float delay)
        { Field("_echoBootsDelaySeconds", delay); Assert.Throws<ArgumentOutOfRangeException>(() => perks.Tick()); }
    }
}
