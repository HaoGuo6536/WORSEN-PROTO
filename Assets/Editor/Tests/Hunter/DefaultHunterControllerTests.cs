// ============================================================================
// DefaultHunterControllerTests.cs
// ============================================================================
// PURPOSE:
//   Proves the shared planner consults archetype utility and movement hooks.
//   Explicit default modules must make the same seeded decisions as the legacy
//   constructor for every retained placeholder key, without modifying old tests.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Protect neutral dispatch and demonstrate a fixture-only override.
// DEPENDENCIES:
//   - Hunter, Player and Core values, the pure World fixture and NUnit.
// USAGE NOTES:
//   Uses temporary configs; does not load or rewrite authored profiles.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Default;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class DefaultHunterControllerTests
    {
        private sealed class FixtureController : DefaultHunterController
        {
            public override float GoalUtility(HunterGoal goal, float utility) => goal == HunterGoal.ProtectExit ? 1000f : 0f;
            public override bool TryMovement(out Vector3 target, out float speed)
            { target = Vector3.left * 7f; speed = 2f; return true; }
        }
        [Test] public void FixtureOverridesSharedGoalUtilityAndMovementTarget()
        {
            var profile = ScriptableObject.CreateInstance<HunterProfile>();
            try
            {
                var player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100, SprintSpeed = 8 };
                var world = new EchoControllerTests.World(); var state = new HunterBehaviorState();
                var controller = new HunterController(state, profile, new System.Random(1), player, world, new FixtureController());
                controller.Reset(new EntityId(-1), Vector3.zero, Vector3.forward); controller.SetFloorView(world);
                var result = controller.Tick(default, .1f, 1);
                Assert.That(state.CurrentGoal, Is.EqualTo(HunterGoal.ProtectExit));
                Assert.That(result.Target, Is.EqualTo(Vector3.left * 7)); Assert.That(result.Speed, Is.EqualTo(2f));
            }
            finally { Object.DestroyImmediate(profile); }
        }
        [TestCase("watcher")] [TestCase("rusher")] [TestCase("lurker")] [TestCase("hexer")] [TestCase("thorncaller")]
        public void DefaultModulePreservesSeededLegacyDecisions(string key)
        {
            var profile = ScriptableObject.CreateInstance<HunterProfile>();
            try
            {
                EchoControllerTests.Tune(profile, "_archetypeKey", key);
                var player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100, SprintSpeed = 8, Position = Vector3.forward * 12 };
                var world = new EchoControllerTests.World(); var a = new HunterBehaviorState(); var b = new HunterBehaviorState();
                var legacy = new HunterController(a, profile, new System.Random(9), player, world);
                var explicitDefault = new HunterController(b, profile, new System.Random(9), player, world, new DefaultHunterController());
                legacy.Reset(new EntityId(-1), Vector3.zero, Vector3.forward); explicitDefault.Reset(new EntityId(-1), Vector3.zero, Vector3.forward);
                for (int tick = 0; tick < 100; tick++)
                {
                    var sight = tick < 40 ? new SightProbe(true, true, true) : default;
                    var left = legacy.Tick(sight, .1f, tick); var right = explicitDefault.Tick(sight, .1f, tick);
                    Assert.That(right.Target, Is.EqualTo(left.Target)); Assert.That(right.Speed, Is.EqualTo(left.Speed));
                    Assert.That(right.Phase, Is.EqualTo(left.Phase)); Assert.That(b.CurrentAction, Is.EqualTo(a.CurrentAction));
                    Assert.That(b.BeliefConfidence, Is.EqualTo(a.BeliefConfidence));
                }
            }
            finally { Object.DestroyImmediate(profile); }
        }
    }
}
