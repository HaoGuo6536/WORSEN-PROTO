// ============================================================================
// MannequinLightingTests.cs
// ============================================================================
// PURPOSE:
//   Proves that lighting evidence cannot gate the unseen Mannequin or be changed
//   by its retired lamp curses. The production room adapter still reports actual
//   lamps accurately, but lit rooms are no longer refuges from this hunter.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), test suite (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve factual room-light characterization without implying safety.
//   - Exercise light-independent rules, Wick, stale views and curse isolation headlessly.
//   - Reject light facts and random failure draws over repeated ticks and resets.
// DEPENDENCIES:
//   - Mannequin rules, Expedition world adapter, Core values and scripted snapshots.
// USAGE NOTES:
//   Managed config shells carry explicit inputs rather than native asset defaults.
//   Occlusion is injected; native camera-cone/physics and motion/contact remain
//   MannequinControllerTests and RosterFunctionalTests coordinator obligations.
// ============================================================================
using System;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Mannequin;
using Worsen.Domain.Player;
using Worsen.Session.Expedition;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class MannequinLightingTests
    {
        private static RosterFunctionalWorld World() => new RosterFunctionalWorld {
            Graph = new LevelGraph(new[] { new LevelRoom(1, Vector3.up * 2f, new Vector3(40, 4, 40)),
                new LevelRoom(2, new Vector3(40, 2, 0), new Vector3(40, 4, 40)) },
                new[] { new LevelEdge(7, 1, 2, true, TraversalAccess.All) }, Array.Empty<LevelAnchor>(), 2, Vector3.right * 40) };
        private static InteractableState Lamp(int id, int room, bool lit) => new InteractableState(id,
            InteractableKind.Light, room, new Vector3(room * 20, 3, 19),
            lit ? InteractableStateValue.Lit : InteractableStateValue.Inactive);

        [Test]
        public void AnyRemainingLampReportsLit_AllOffReportsKnownDarkness_NotSafety()
        {
            var level = World();
            level.Lights = new[] { Lamp(11, 1, true), Lamp(12, 1, true), Lamp(21, 2, true) };
            var view = new ExpeditionHunterController(new ExpeditionHunterBehaviorState(), 1, level.Graph, level);
            Assert.That(view.TryGetRoomLit(1, out bool lit), Is.True); Assert.That(lit, Is.True);
            level.Lights = new[] { Lamp(11, 1, false), Lamp(12, 1, true), Lamp(21, 2, true) };
            Assert.That(view.TryGetRoomLit(1, out lit), Is.True); Assert.That(lit, Is.True,
                "Partial lamp removal must not redefine factual room lighting.");
            level.Lights = new[] { Lamp(11, 1, false), Lamp(12, 1, false), Lamp(21, 2, true) };
            Assert.That(view.TryGetRoomLit(1, out lit), Is.True); Assert.That(lit, Is.False);
            Assert.That(view.TryGetRoomLit(2, out lit), Is.True); Assert.That(lit, Is.True,
                "Changing one room must not also extinguish its neighbour.");
        }

        [Test]
        public void KnownRoomWithoutLampsIsDark_UnknownRoomOrMissingWorldIsNotDarkEvidence()
        {
            var level = World();
            var view = new ExpeditionHunterController(new ExpeditionHunterBehaviorState(), 1, level.Graph, level);
            Assert.That(view.TryGetRoomLit(1, out bool lit), Is.True); Assert.That(lit, Is.False);
            Assert.That(view.TryGetRoomLit(999, out _), Is.False);
            view = new ExpeditionHunterController(new ExpeditionHunterBehaviorState(), 1, null, level);
            Assert.That(view.TryGetRoomLit(1, out _), Is.False);
            view = new ExpeditionHunterController(new ExpeditionHunterBehaviorState(), 1, level.Graph, null);
            Assert.That(view.TryGetRoomLit(1, out _), Is.False);
        }

        private static MannequinController Rules(System.Random random = null)
        {
            var config = (MannequinConfig)FormatterServices.GetUninitializedObject(typeof(MannequinConfig));
            EchoControllerTests.Tune(config, "_observationHeight", 1f);
            EchoControllerTests.Tune(config, "_directLookHalfAngle", 12f);
            EchoControllerTests.Tune(config, "_longerStridesMultiplier", 1.2f);
            return new MannequinController(config, random ?? new System.Random(23));
        }
        private static HunterArchetypeContext Context(long tick, IReadOnlyActiveEffects effects = null, float dt = .1f) =>
            new HunterArchetypeContext(new RosterBTestHunter { Id = new Worsen.Core.EntityId(-1), IsActive = true },
                new PlayerBehaviorState { Id = new Worsen.Core.EntityId(1), Health = 100, SprintSpeed = 8, Position = Vector3.forward * 10 },
                null, null, null, null, effects, dt, tick, true, 1f);
        private static HunterPlayerView View(long tick) => new HunterPlayerView(new Vector3(0, 1, 10), Quaternion.identity, 90, 60, tick);

        [TestCase(false, false)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(true, true)]
        public void OccludedMannequinPursuesIndependentlyOfRoomAndDirectLight(bool lit, bool illuminated)
        {
            var rules = Rules(); rules.Reset(Context(0));
            var level = World(); level.Lights = new[] { Lamp(11, 1, lit) };
            var world = new ExpeditionHunterController(new ExpeditionHunterBehaviorState(), 1, level.Graph, level);
            Assert.That(world.TryGetRoomLit(1, out bool actual), Is.True); Assert.That(actual, Is.EqualTo(lit));
            rules.Observe(View(1), false, illuminated, world, false); rules.Tick(Context(1));
            Assert.That(rules.Hold, Is.False); Assert.That(rules.Silent, Is.True);
            Assert.That(rules.OwnsPursuit, Is.True, "Shared flashlight reactions must not override this rule.");
            Assert.That(rules.FilterVisibility(false, default, Context(1)), Is.True,
                "Hunter sight/heading cannot prevent pursuit when the player cannot see it.");
            Assert.That(rules.GoalUtility(HunterGoal.LocatePrey, 100f), Is.EqualTo(100f));
            Assert.That(rules.GoalUtility(HunterGoal.DenyCake, 200f), Is.Zero);
            Assert.That(rules.GoalUtility(HunterGoal.ProtectExit, 200f), Is.Zero);
            Assert.That(rules.GoalUtility(HunterGoal.BreakLoop, 200f), Is.Zero);
        }

        [Test] public void WickAndStaleCameraStillHoldWithoutLightingOrRoomData()
        {
            var rules = Rules(); rules.Reset(Context(0)); Assert.That(rules.Hold, Is.True);
            rules.Observe(View(1), false, true, null, false); rules.Tick(Context(1)); Assert.That(rules.Hold, Is.False);
            rules.Observe(View(2), false, false, null, true);
            Assert.That(rules.Hold, Is.True, "Wick must gate contact immediately, even before Tick.");
            rules.Tick(Context(2)); Assert.That(rules.FilterVisibility(true, default, Context(2)), Is.False);
            rules.Observe(View(3), false, false, null, false); rules.Tick(Context(3)); Assert.That(rules.Hold, Is.False);
            rules.Tick(Context(4)); Assert.That(rules.Hold, Is.True);
            rules.Observe(default, false, false, null, false); rules.Tick(Context(5)); Assert.That(rules.Hold, Is.True);
        }

        [Test] public void LightCursesAndAfterglowAreInertAndNoFailureDrawsOrLightFactsRemain()
        {
            var random = new System.Random(23); var expected = new System.Random(23);
            var rules = Rules(random);
            var effects = new ActiveEffects(new[] {
                new ActiveEffect(new EffectId("mannequin-fewer-lamps"), EffectKind.Curse, 99),
                new ActiveEffect(new EffectId("mannequin-broken-lights"), EffectKind.Curse, 99),
                new ActiveEffect(new EffectId("afterglow"), EffectKind.Upgrade, 1) });
            rules.Reset(Context(0, effects)); rules.SetEffects(effects);
            int facts = 0;
            for (int tick = 1; tick <= 1000; tick++)
            {
                Assert.That(rules.BeginAfterglow(1), Is.Zero);
                rules.Observe(View(tick), false, true, null, false); rules.Tick(Context(tick, effects, 15f));
                Assert.That(rules.Hold, Is.False); Assert.That(rules.SpeedMultiplier, Is.EqualTo(1f));
                while (rules.TakeFact(out var fact)) { facts++; Assert.That(fact.Kind, Is.EqualTo(MannequinFactKind.SilentSoundSet)); }
            }
            Assert.That(facts, Is.EqualTo(1)); Assert.That(random.NextDouble(), Is.EqualTo(expected.NextDouble()));
            Assert.That(rules.TryCatch(), Is.True); Assert.That(rules.TryCatch(), Is.False);
            rules.Reset(Context(0)); Assert.That(rules.Hold, Is.True); Assert.That(rules.TryCatch(), Is.True);
            Assert.That(rules.TakeFact(out var reset), Is.True); Assert.That(reset.Kind, Is.EqualTo(MannequinFactKind.SilentSoundSet));
            Assert.That(rules.TakeFact(out _), Is.False);
        }

        [Test] public void LongerStridesCapsAndRemovesWithoutLeakingAcrossInstances()
        {
            var a = Rules(); var b = Rules(); a.Reset(Context(0)); b.Reset(Context(0));
            var effects = new ActiveEffects(new[] { new ActiveEffect(new EffectId("mannequin-longer-strides"), EffectKind.Curse, 99) });
            a.Observe(View(1), false, false, null, false); b.Observe(View(1), false, false, null, false);
            a.Tick(Context(1, effects)); b.Tick(Context(1));
            Assert.That(a.SpeedMultiplier, Is.EqualTo(Mathf.Pow(1.2f, 3)).Within(.0001f));
            Assert.That(b.SpeedMultiplier, Is.EqualTo(1f));
            a.Observe(View(2), false, false, null, false); a.Tick(Context(2));
            Assert.That(a.SpeedMultiplier, Is.EqualTo(1f));
        }

        [Test] public void SharedPipelineRequestsPursuitWithoutSightOrLight_AndWickCancelsMomentum()
        {
            var rules = Rules(); var state = new HunterBehaviorState();
            var profile = HunterAttackControllerTests.Profile();
            EchoControllerTests.Tune(profile, "_sightRange", 30f);
            EchoControllerTests.Tune(profile, "_sightConeDegrees", 110f);
            EchoControllerTests.Tune(profile, "_sensorIntervalTicks", 1);
            EchoControllerTests.Tune(profile, "_lungeDistance", 4f);
            EchoControllerTests.Tune(profile, "_memoryDecaySeconds", 8f);
            EchoControllerTests.Tune(profile, "_lightResponse", HunterLightResponse.Avoid);
            var player = new PlayerBehaviorState { Id = new Worsen.Core.EntityId(1), Health = 100, SprintSpeed = 8, Position = Vector3.forward * 10 };
            var shared = new HunterController(state, profile, new System.Random(7), player,
                new EchoControllerTests.World { Graph = null }, rules);
            shared.Reset(new Worsen.Core.EntityId(-1), Vector3.zero, Vector3.back);
            shared.SetPlayerView(View(1)); shared.ObservePlayerView(false);
            var move = shared.Tick(default, new HunterLightObservation(true, true, Vector3.left * 5, 1), .1f, 1);
            Assert.That(move.HoldPosition, Is.False); Assert.That(move.Speed, Is.GreaterThan(0f));
            Assert.That(move.Target, Is.EqualTo(player.Position)); Assert.That(state.CurrentAction, Is.EqualTo(HunterAction.Chase));
            shared.CommitPose(Vector3.forward, Vector3.forward * move.Speed, Vector3.forward);
            shared.SetWickActive(true); shared.SetPlayerView(View(2));
            var held = shared.Tick(default, .1f, 2);
            Assert.That(held.HoldPosition, Is.True); Assert.That(held.Speed, Is.Zero);
            Assert.That(state.Velocity, Is.EqualTo(Vector3.zero));
            Assert.That(shared.TryAcceptContact(player.Id, out _), Is.False);
            Assert.That(shared.TryDequeueFeedback(out _), Is.False);
            shared.SetWickActive(false); shared.SetPlayerView(View(3));
            Assert.That(shared.Tick(default, .1f, 3).Speed, Is.GreaterThan(0f));
        }
    }
}
