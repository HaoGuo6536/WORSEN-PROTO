// ============================================================================
// PlayerConsumableTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the small Player APIs used by Session consumables and Extra Life.
//   Explicit healing seconds and speed factors keep effect lifetimes outside Player.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · Player.
// KEY RESPONSIBILITIES:
//   - Use the sole timed web contact API and ensure cleansing cancels its lifetime.
//   - Cover healing clamps, speed composition, cleansing and half-health floor respawn.
//   - Verify in-place revival, independent protection deadlines, tuning and resets.
//   - Verify revival clears stale perk/motion state while retaining inventory/effects.
// DEPENDENCIES:
//   - Player pure controller/state/profile, Core values and NUnit.
// USAGE NOTES:
//   Edit Mode; profile objects are temporary and never saved.
// ============================================================================
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Player
{
    public sealed class PlayerConsumableTests
    {
        private PlayerProfile profile;
        private PlayerController player;
        private PlayerBehaviorState state;
        [SetUp] public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<PlayerProfile>(); state = new PlayerBehaviorState();
            player = new PlayerController(state, profile, new System.Random(1));
            player.Reset(new EntityId(1), new Vector3(2f, 0f, 3f), 45f);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(profile);
        [Test]
        public void HealingUsesMovingSecondsClampsAndCannotResurrect()
        {
            player.ApplyRunModifiers(50f, 100f, 1f);
            Assert.That(player.HealOverTime(8.75f, 0f), Is.False); Assert.That(state.Health, Is.EqualTo(50f));
            Assert.That(player.HealOverTime(8.75f, 4f), Is.True); Assert.That(state.Health, Is.EqualTo(85f));
            player.HealOverTime(100f, 1f); Assert.That(state.Health, Is.EqualTo(100f));
            player.ApplyRunModifiers(0f, 100f, 1f);
            Assert.That(player.HealOverTime(100f, 1f), Is.False); Assert.That(state.IsAlive, Is.False);
        }
        [Test]
        public void BurstMultipliesSpeedWhileSaltsClearWebAndTrapButNotGrab()
        {
            float baseline = player.MaximumMovementSpeed;
            player.SetTrapSpeedMultiplier(.5f); player.ApplyWebSlow(new WebHitFact(new EntityId(-1), state.Id, 0, 1, .5f, 3f, 1f)); player.SetGrabSpeedMultiplier(.5f);
            player.SetConsumableSpeedMultiplier(1.5f);
            Assert.That(player.MaximumMovementSpeed, Is.EqualTo(baseline * .5f * .5f * .5f * 1.5f).Within(.001f));
            player.ClearSlows(); Assert.That(player.MaximumMovementSpeed, Is.EqualTo(baseline * .5f * 1.5f).Within(.001f));
            Assert.That(state.WebSlowRemaining, Is.Zero); Assert.That(state.WebSpeedMultiplier, Is.EqualTo(1f));
            player.ApplyWebSlow(new WebHitFact(new EntityId(-1), state.Id, 1, 2, .8f, .1f, 1f));
            Assert.That(state.WebSpeedMultiplier, Is.EqualTo(.8f));
            player.Tick(default, default, .2f, 2);
            Assert.That(state.WebSlowRemaining, Is.Zero); Assert.That(state.WebSpeedMultiplier, Is.EqualTo(1f));
            player.SetConsumableSpeedMultiplier(1f); Assert.That(player.MaximumMovementSpeed, Is.EqualTo(baseline * .5f).Within(.001f));
        }
        [TestCase(50)] [TestCase(60)] [TestCase(120)]
        public void InPlaceRevivalKeepsPoseAndModifiersWithIndependentEndExclusiveProtection(int rate)
        {
            player.Reset(new EntityId(1), Vector3.zero, 0f, 1f / rate);
            player.ApplyRunModifiers(0f, 120f, 1.2f);
            state.Position = new Vector3(20f, 4f, 30f); state.HeadingDegrees = 123f;
            state.Tick = 900; state.Crouched = true; state.Grounded = true;
            player.SetHealthRecoveryEffects(0f);
            Assert.That(player.ReviveInPlace(.25f), Is.True);
            Assert.That(state.Position, Is.EqualTo(new Vector3(20f, 4f, 30f)));
            Assert.That(state.HeadingDegrees, Is.EqualTo(123f)); Assert.That(state.Crouched, Is.True);
            Assert.That(state.Health, Is.EqualTo(30f)); Assert.That(state.MaxHealth, Is.EqualTo(120f));
            Assert.That(state.MovementSpeedMultiplier, Is.EqualTo(1.2f));
            Assert.That(state.RegenerationMultiplier, Is.Zero); Assert.That(state.HitBoostMultiplier, Is.EqualTo(1f));
            Assert.That(player.ReviveInPlace(.5f), Is.False, "No duplicate revival or extended deadline.");
            Assert.That(player.GrantShield(10f), Is.True);
            player.AdvanceRecovery(900 + rate * 2 - 1);
            Assert.That(state.RevivalCollisionGraceActive, Is.True);
            Assert.That(player.ApplyHit(1000f).Changed, Is.False);
            Assert.That(player.AdvanceRecovery(900 + rate * 2), Is.Null);
            Assert.That(state.RevivalCollisionGraceActive, Is.False);
            Assert.That(state.RevivalDamageImmune, Is.True); Assert.That(state.IsUngrabbable, Is.True);
            player.AdvanceRecovery(900 + rate * 3 - 1);
            Assert.That(player.ApplyHit(1000f).Changed, Is.False);
            Assert.That(state.Health, Is.EqualTo(30f)); Assert.That(state.Shield, Is.EqualTo(10f));
            var ended = player.AdvanceRecovery(900 + rate * 3);
            Assert.That(ended.HasValue, Is.True); Assert.That(ended.Value.StartTick, Is.EqualTo(900));
            Assert.That(state.RevivalDamageImmune, Is.False); Assert.That(state.IsUngrabbable, Is.False);
            Assert.That(player.AdvanceRecovery(900 + rate * 3), Is.Null);
            Assert.That(player.ApplyHit(20f).Changed, Is.True);
            Assert.That(state.Shield, Is.Zero); Assert.That(state.Health, Is.EqualTo(20f));
        }

        [TestCase(false)] [TestCase(true)]
        public void FloorOrNewLifeClearsRevivalProtection(bool newLife)
        {
            player.ApplyRunModifiers(0f, 100f, 1f); player.ReviveInPlace(.5f);
            if (newLife) player.Reset(new EntityId(2), Vector3.zero, 0f);
            else player.BeginFloorHealth(100f, 1f);
            Assert.That(state.RevivalCollisionGraceActive, Is.False);
            Assert.That(state.RevivalDamageImmune, Is.False); Assert.That(state.IsUngrabbable, Is.False);
            Assert.That(player.ApplyHit(1f).Changed, Is.True);
        }

        [Test]
        public void RevivalTuningIsIndependentOfOrdinaryGraceAndBoostEffects()
        {
            typeof(PlayerProfile).GetField("_revivalCollisionGraceSeconds", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(profile, .5f);
            typeof(PlayerProfile).GetField("_revivalDamageImmunitySeconds", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(profile, 0f);
            player.Reset(new EntityId(1), Vector3.zero, 0f, .25f);
            player.ApplyRunModifiers(0f, 100f, 1f); player.ReviveInPlace(.5f);
            Assert.That(state.RevivalCollisionGraceActive, Is.True); Assert.That(state.RevivalDamageImmune, Is.False);
            Assert.That(state.HitBoostMultiplier, Is.EqualTo(1f));
            player.AdvanceRecovery(1); Assert.That(state.RevivalCollisionGraceActive, Is.True);
            player.AdvanceRecovery(2); Assert.That(state.RevivalCollisionGraceActive, Is.False);
        }

        [TestCase(0f)] [TestCase(-1f)] [TestCase(1.1f)] [TestCase(float.NaN)]
        public void InvalidRevivalFractionCannotChangeDeathOrProtection(float fraction)
        {
            player.ApplyRunModifiers(0f, 100f, 1f);
            Assert.That(player.ReviveInPlace(fraction), Is.False);
            Assert.That(state.IsAlive, Is.False); Assert.That(state.RevivalCollisionGraceActive, Is.False);
            Assert.That(state.RevivalDamageImmune, Is.False);
        }

        [Test]
        public void RevivalClearsContactsNoisesAndPendingThrowButPreservesInventoryAndActiveEffects()
        {
            var config = ScriptableObject.CreateInstance<PlayerEffectConfig>();
            try
            {
                player = new PlayerController(state, profile, new System.Random(1), config);
                player.Reset(new EntityId(1), Vector3.zero, 0f);
                var effects = new ActiveEffects(new[] { new ActiveEffect(new EffectId("loud-heart"), EffectKind.Upgrade, 1) });
                player.SetActiveEffects(effects);
                player.ReceiveChase(new ChaseFact(1, state.Id, new EntityId(-1), 0, ChasePhase.Confirmed));
                player.Tick(default, new MovementProbe(true, Vector3.up), .01f, 1);
                Assert.That(player.TakeHeartbeat(out _), Is.True);
                state.Inventory = new InventorySnapshot("bandage-roll", "firecracker");
                state.Position = new Vector3(10f, 3f, 20f); state.HeadingDegrees = 91f;
                player.ApplyExternalVelocity(Vector3.one, ExternalMotionKind.Impulse); player.ApplyHit(1000f);
                Assert.That(player.ReviveInPlace(.5f), Is.True);
                Assert.That(state.Inventory, Is.EqualTo(new InventorySnapshot("bandage-roll", "firecracker")));
                Assert.That(state.Position, Is.EqualTo(new Vector3(10f, 3f, 20f)));
                Assert.That(state.HeadingDegrees, Is.EqualTo(91f)); Assert.That(state.ActiveEffects, Is.EqualTo(effects));
                Assert.That(state.AppliedEffects.Has(new EffectId("loud-heart")), Is.True);
                Assert.That(state.PendingExternalVelocity, Is.EqualTo(Vector3.zero));
                Assert.That(state.Velocity, Is.EqualTo(Vector3.zero)); Assert.That(state.RecentNoises, Is.Empty);
                Assert.That(state.LastTraversalFacts, Is.Empty); Assert.That(state.HitBoostMultiplier, Is.EqualTo(1f));
                player.Tick(default, default, .01f, 2);
                Assert.That(player.TakeHeartbeat(out _), Is.False, "The pre-death chase must not survive revival.");
                Assert.That(player.ApplyHit(1000f).AbsorbedByGrace, Is.True);
                player.EndRecovery(); Assert.That(player.ApplyHit(1000f).Died, Is.True);
                Assert.That(player.HealOverTime(1000f, 1f), Is.False, "Session, not healing, owns the one-use Extra Life authorization.");
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void RevivalRestoresFloorStartAtHalfEffectiveMaximumWithoutStartingANewFloor()
        {
            Vector3 start = state.Position;
            player.ApplyRunModifiers(0f, 120f, 1.2f); state.Position = new Vector3(20f, 0f, 30f); state.Tick = 900;
            Assert.That(player.RespawnAtFloorStart(.5f), Is.True);
            Assert.That(state.Position, Is.EqualTo(start)); Assert.That(state.Health, Is.EqualTo(60f));
            Assert.That(state.MaxHealth, Is.EqualTo(120f)); Assert.That(state.HeadingDegrees, Is.EqualTo(45f));
            Assert.That(state.Tick, Is.EqualTo(900)); Assert.That(state.IsAlive, Is.True);
            Assert.That(player.RespawnAtFloorStart(.5f), Is.False); Assert.That(state.Health, Is.EqualTo(60f));
        }
    }
}
