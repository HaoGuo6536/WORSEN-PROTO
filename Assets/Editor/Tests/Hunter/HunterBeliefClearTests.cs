// ============================================================================
// HunterBeliefClearTests.cs
// ============================================================================
// PURPOSE:
//   Exercises Pacification independently of physics and the Hunter tick owner.
//   Belief loss must erase deferred decisions without interrupting committed attacks.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Hunter.
// KEY RESPONSIBILITIES:
//   - Check pursuit/search/noise clearing, repeated clearing and active contact continuity.
// DEPENDENCIES:
//   - Hunter logic, Player/Level read-only data, Core, NUnit and test-only reflection.
// USAGE NOTES:
//   Injected random/time; no scene, navigation bake or live Hunter driver.
// ============================================================================
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using Worsen.Domain.Level;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HunterBeliefClearTests
    {
        [TestCase(false)] [TestCase(true)]
        public void ClearDropsDecisionsButPreservesCommittedLunge(bool lunge)
        {
            var profile = ScriptableObject.CreateInstance<HunterProfile>();
            try
            {
                var player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100f, SprintSpeed = 8f, Position = Vector3.forward * 3f };
                var state = new HunterBehaviorState();
                var controller = new HunterController(state, profile, new System.Random(17), player, new LevelBehaviorState());
                controller.Reset(new EntityId(-1), Vector3.zero, Vector3.forward);
                controller.Tick(new SightProbe(true, true, true), .02f, 1);
                if (lunge) controller.Tick(default, profile.LungeWindupSeconds, 2);
                else typeof(HunterBehaviorState).GetProperty("LungePhase").SetValue(state, HunterLungePhase.None);
                Set(state, "PendingNoiseDecision", true); Set(state, "DeliberationRemaining", 1f);
                Set(state, "SearchActive", true); Set(state, "Predict", true);
                ((IList)Get(state, "SearchRoute")).Add(Vector3.one);
                ((IList)Get(state, "PredictionRoute")).Add(1);
                var phase = state.LungePhase; var direction = state.LungeDirection; var seconds = state.PhaseSeconds;
                controller.ClearBelief(); controller.ClearBelief();
                Assert.That(state.BeliefConfidence, Is.Zero); Assert.That(state.PlayerVisible, Is.False);
                Assert.That(state.LastKnownPosition, Is.EqualTo(state.Position)); Assert.That(state.CurrentTarget, Is.EqualTo(state.Position));
                Assert.That(state.IsDeliberating, Is.False);
                foreach (string field in new[] { "PendingNoiseDecision", "SearchActive", "Predict", "HasHint", "PlayerHeard" })
                    Assert.That(Get(state, field), Is.False, field);
                Assert.That((IList)Get(state, "SearchRoute"), Is.Empty); Assert.That((IList)Get(state, "PredictionRoute"), Is.Empty);
                Assert.That(state.LungePhase, Is.EqualTo(phase)); Assert.That(state.LungeDirection, Is.EqualTo(direction));
                Assert.That(state.PhaseSeconds, Is.EqualTo(seconds));
                if (lunge) Assert.That(controller.TryAcceptContact(player.Id, out _), Is.True);
                else Assert.That(state.CurrentAction, Is.EqualTo(HunterAction.Patrol));
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
        }
        private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
