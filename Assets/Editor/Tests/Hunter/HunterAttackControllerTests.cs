// ============================================================================
// HunterAttackControllerTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the extracted attack seam in a standalone managed process.
//   Config fields are explicit fixture inputs rather than native assets, following
//   the existing PlayerPerk tests. Original Hunter tests still check Unity defaults.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Verify committed heading, phase carry, feedback order and missed-lunge stumble.
//   - Verify contact deduplication, interruption gates and revival admission.
// DEPENDENCIES:
//   - Hunter attack logic/state, Player state, Core values, reflection and NUnit.
// USAGE NOTES:
//   No native object creation or test skips. Reflection seeds internal per-life
//   state without widening production setters. All random draws use a fixed seed.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HunterAttackControllerTests
    {
        internal static HunterProfile Profile()
        {
            var profile = (HunterProfile)FormatterServices.GetUninitializedObject(typeof(HunterProfile));
            Set(profile, "_archetypeKey", "fixture"); Set(profile, "_lungeWindupSeconds", .25f);
            Set(profile, "_lungeActiveSeconds", .25f); Set(profile, "_lungeRecoverySeconds", .5f);
            Set(profile, "_missStaggerSeconds", .5f); Set(profile, "_missStumbleMeters", 1f);
            Set(profile, "_lungeDamage", 50); Set(profile, "_deliberationSeconds", .5f);
            Set(profile, "_acceleration", 20f); Set(profile, "_turnRate", 240f); Set(profile, "_chaseSpeedMultiplier", 1f);
            Set(profile, "_actionCommitmentSeconds", .5f); Set(profile, "_investigateSpeed", 2f);
            Set(profile, "_arrivalRadius", .1f); Set(profile, "_searchLegTimeoutSeconds", 1f);
            Set(profile, "_searchMaximumLegSeconds", 10f); Set(profile, "_searchTravelAllowance", 1f);
            Set(profile, "_lightResponse", HunterLightResponse.Investigate);
            Set(profile, "_habits", new[] { new HunterHabitData(HunterHabitKind.ThresholdPause),
                new HunterHabitData(HunterHabitKind.TurnToFace), new HunterHabitData(HunterHabitKind.CakeReaction) });
            return profile;
        }
        internal static void Set(object target, string name, object value)
        {
            var type = target.GetType();
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) field.SetValue(target, value);
            else type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        }
        internal static T Get<T>(object target, string name) => (T)target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
        internal static HunterBehaviorState State()
        {
            var state = new HunterBehaviorState(); Set(state, "Id", new EntityId(-1)); Set(state, "TargetId", new EntityId(1));
            Set(state, "IsActive", true); Set(state, "Forward", Vector3.forward); Set(state, "PlayerVisible", true);
            Set(state, "Action", HunterAction.Lunge); return state;
        }
        private HunterBehaviorState state;
        private HunterProfile profile;
        private PlayerBehaviorState player;
        private HunterAttackController attack;
        [SetUp] public void Setup()
        {
            state = State(); profile = Profile(); player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100, Position = Vector3.forward * 2, SprintSpeed = 8 };
            attack = new HunterAttackController(state, profile, new System.Random(5), player, new HunterArchetypeController());
        }
        [Test] public void LargeStepCarriesPhasesAndPreservesFeedbackOrder()
        {
            Assert.That(attack.TryBegin(), Is.True); player.Position = Vector3.left * 5;
            Vector3 stumble = attack.Advance(.75f, out float seconds);
            Assert.That(state.LungePhase, Is.EqualTo(HunterLungePhase.Recovery));
            Assert.That(state.PhaseSeconds, Is.EqualTo(.25f)); Assert.That(seconds, Is.EqualTo(.25f));
            Assert.That(stumble, Is.EqualTo(Vector3.forward * .5f)); Assert.That(state.LungeDirection, Is.EqualTo(Vector3.forward));
            Assert.That(Get<Queue<HunterFeedbackKind>>(state, "Feedback").ToArray(), Is.EqualTo(new[] {
                HunterFeedbackKind.AttackWindup, HunterFeedbackKind.AttackSwing, HunterFeedbackKind.AttackMiss, HunterFeedbackKind.AttackRecovery }));
            attack.Advance(.25f, out seconds); Assert.That(state.LungePhase, Is.EqualTo(HunterLungePhase.None));
            Assert.That(seconds, Is.EqualTo(.25f));
        }
        [Test] public void MeleeContactIsOnceOnlyAndDoesNotSpendLatchOnWrongTarget()
        {
            attack.TryBegin(); attack.Advance(.25f, out _);
            Assert.That(attack.TryAcceptContact(new EntityId(2), out _), Is.False);
            Assert.That(attack.TryAcceptContact(player.Id, out var hit), Is.True); Assert.That(hit.Damage, Is.EqualTo(50));
            Assert.That(attack.TryAcceptContact(player.Id, out _), Is.False);
            Assert.That(attack.Advance(.5f, out float seconds), Is.EqualTo(Vector3.zero)); Assert.That(seconds, Is.Zero);
        }
        [Test] public void CatchAndReactionRejectWithoutSpendingContact()
        {
            attack.TryBegin(); attack.Advance(.25f, out _); Set(state, "CatchActive", true);
            Assert.That(attack.TryAcceptContact(player.Id, out _), Is.False); Set(state, "CatchActive", false);
            Set(state, "ReactionHeld", true); Assert.That(attack.TryAcceptContact(player.Id, out _), Is.False);
            Set(state, "ReactionHeld", false); Assert.That(attack.TryAcceptContact(player.Id, out _), Is.True);
        }
        [Test] public void RangedSerialRequiresFiringAndDeduplicatesIndependently()
        {
            Set(profile, "_attackStyle", HunterAttackStyle.Projectile); attack.TryBegin();
            Assert.That(attack.TryAcceptRangedContact(player.Id, state.AttackSerial, out _), Is.False);
            attack.Advance(.25f, out _);
            Assert.That(attack.TryAcceptRangedContact(player.Id, state.AttackSerial, out _), Is.True);
            Assert.That(attack.TryAcceptRangedContact(player.Id, state.AttackSerial, out _), Is.False);
            Assert.That(attack.TryAcceptRangedContact(player.Id, state.AttackSerial + 1, out _), Is.False);
        }
    }
}
