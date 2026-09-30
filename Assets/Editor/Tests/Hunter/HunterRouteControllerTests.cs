// ============================================================================
// HunterRouteControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies route commitments independently of the Hunter tick coordinator.
//   Explicit managed profile fields distinguish observation memory from hidden
//   player pose and exercise bounded search exhaustion without a navigation bake.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Verify search budgets, memory targets and elevated-melee planning admission.
// DEPENDENCIES:
//   - Hunter route/habit logic, Core values, Player state and managed test inputs.
// USAGE NOTES:
//   No native object creation. Existing HunterIntelligenceTests remain unchanged
//   and cover full-tick prediction and floor-goal integration under Unity.
// ============================================================================
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using static Worsen.Tests.Hunter.HunterAttackControllerTests;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HunterRouteControllerTests
    {
        private HunterBehaviorState state;
        private HunterProfile profile;
        private PlayerBehaviorState player;
        private HunterRouteController routes;
        [SetUp] public void Setup()
        {
            state = State(); profile = Profile(); player = new PlayerBehaviorState { Health = 100, SprintSpeed = 8, Position = Vector3.forward * 2 };
            var world = new EchoControllerTests.World(); var rules = new HunterArchetypeController();
            routes = new HunterRouteController(state, profile, new System.Random(11), player, world, rules,
                new HunterHabitController(state, profile, player, world, rules));
        }
        [Test] public void StalkAndUnpredictedChaseUseMemoryNotHiddenPlayerPose()
        {
            Set(state, "LastKnownPosition", Vector3.left * 3); Set(state, "Action", HunterAction.Stalk);
            routes.UpdateTarget(.1f, null); Assert.That(state.CurrentTarget, Is.EqualTo(Vector3.left * 3));
            Set(state, "Action", HunterAction.Chase); routes.UpdateTarget(.1f, null);
            Assert.That(state.CurrentTarget, Is.EqualTo(Vector3.left * 3));
        }
        [Test] public void SearchLegHasTravelAllowanceAndEventuallyExhaustsBelief()
        {
            Set(state, "Action", HunterAction.SearchLastKnown); Set(state, "BeliefConfidence", 1f);
            Get<List<Vector3>>(state, "SearchRoute").Add(Vector3.forward * 4);
            routes.UpdateTarget(2f, null); Assert.That(Get<float>(state, "SearchLegBudget"), Is.EqualTo(3f));
            Assert.That(state.CurrentAction, Is.EqualTo(HunterAction.SearchLastKnown));
            routes.UpdateTarget(1f, null); Assert.That(state.CurrentAction, Is.EqualTo(HunterAction.Patrol));
            Assert.That(state.BeliefConfidence, Is.Zero); Assert.That(Get<List<Vector3>>(state, "SearchRoute"), Is.Empty);
        }
        [Test] public void MeleePlanningRequiresReachableElevation()
        {
            routes.Replan(null, 0f, 4f, 110f); Assert.That(state.CurrentAction, Is.EqualTo(HunterAction.Lunge));
            player.Position += Vector3.up * 2; routes.Replan(null, 0f, 4f, 110f);
            Assert.That(state.CurrentAction, Is.EqualTo(HunterAction.Chase));
        }
        [Test] public void CommittedPredictionIsNotReplacedByHiddenPoseDuringTargetUpdate()
        {
            Set(state, "Action", HunterAction.Chase); Set(state, "Predict", true); Set(state, "NavigationTarget", Vector3.right * 8);
            routes.UpdateTarget(.1f, null); Assert.That(state.CurrentTarget, Is.EqualTo(Vector3.right * 8));
        }
    }
}
