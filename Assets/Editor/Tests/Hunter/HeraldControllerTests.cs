// ============================================================================
// HeraldControllerTests.cs
// ============================================================================
// PURPOSE:
//   Tests fixed Herald audio, radius hits and floor-wide Director delivery.
//   Injected time and random seeds make the scream sequence reproducible, while
//   real Manager delivery checks prevent ordinary hearing range from hiding a call.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Cover every scream, curse caps, timing tell and independent attack ownership.
// DEPENDENCIES:
//   - Hunter/Director, Core facts, NUnit and the existing pure world fixture.
// USAGE NOTES:
//   Coordinator runs Edit Mode tests. Registry actors are explicitly cleaned up;
//   no Unity clock, navigation bake, scene save or Session implementation edit.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Director;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Herald;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HeraldControllerTests
    {
        private HeraldConfig config;
        private HunterProfile profile;
        private HeraldController module;
        private BlinderControllerTests.HunterView hunter;
        private PlayerBehaviorState player;
        private EchoControllerTests.World world;
        private IReadOnlyActiveEffects effects;
        private long tick;
        private HunterArchetypeContext Context(float dt = .1f, bool allowed = true) => new HunterArchetypeContext(
            hunter, player, world, world, world.Doors, world, effects, dt, tick, allowed, 1.02f);
        [SetUp] public void Setup()
        {
            config = ScriptableObject.CreateInstance<HeraldConfig>(); profile = ScriptableObject.CreateInstance<HunterProfile>();
            hunter = new BlinderControllerTests.HunterView();
            player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100, SprintSpeed = 8f, Position = Vector3.forward * 8f };
            world = new EchoControllerTests.World(); effects = default(ActiveEffects); tick = 0;
            module = new HeraldController(new HeraldBehaviorState(), config, new System.Random(7)); module.Reset(Context());
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(config); Object.DestroyImmediate(profile); }
        private void Step(float dt = .1f, bool allowed = true)
        { tick++; module.Tick(Context(dt, allowed)); module.ResolveAfterSensing(); }
        private HeraldScreamFact Scream()
        { Assert.That(module.TakeScream(out var fact), Is.True); Assert.That(module.TakeScream(out _), Is.False); return fact; }
        [Test] public void FixedFilesAlternateWithSeededPitchAndRandomSpacing()
        {
            Step(); var discovery = Scream(); Assert.That(discovery.SoundId, Is.EqualTo(HeraldController.DiscoveryClip));
            Step(4f); var first = Scream(); Step(4f); var second = Scream();
            Assert.That(first.SoundId, Is.EqualTo(HeraldController.ChaseOneClip)); Assert.That(second.SoundId, Is.EqualTo(HeraldController.ChaseTwoClip));
            foreach (var fact in new[] { discovery, first, second })
            { Assert.That(fact.Pitch, Is.InRange(.94f, 1.06f)); Assert.That(fact.Noise.SourceKind, Is.EqualTo(NoiseSourceKind.Scream));
                Assert.That(fact.FloorWideHint.Source, Is.EqualTo(hunter.Id)); Assert.That(fact.FloorWideHint.Position, Is.EqualTo(hunter.LastKnownPosition)); }
            module.Reset(Context()); tick = 0; module = new HeraldController(new HeraldBehaviorState(), config, new System.Random(7)); module.Reset(Context());
            Step(); Assert.That(Scream().Pitch, Is.EqualTo(discovery.Pitch)); Step(2.49f); Assert.That(module.TakeScream(out _), Is.False);
        }
        [TestCase(7f, true)] [TestCase(7.01f, false)]
        public void WarnedAttackUsesInclusiveRadiusLowDamageDurationAndFixedPitch(float distance, bool hitExpected)
        {
            player.Position = Vector3.forward * 6f; Step(); Scream(); Step();
            Assert.That(module.TakeBreath(out var breath), Is.True); Assert.That(breath.Duration, Is.EqualTo(.8f));
            Assert.That(module.Hold, Is.True); Step(.79f); Assert.That(module.TakeScream(out _), Is.False);
            player.Position = Vector3.forward * distance; Step(.01f); var attack = Scream();
            Assert.That(attack.SoundId, Is.EqualTo(HeraldController.AttackClip)); Assert.That(attack.Pitch, Is.EqualTo(1f));
            Assert.That(module.TakeHit(out var hit), Is.EqualTo(hitExpected));
            if (hitExpected)
            { Assert.That(hit.Damage, Is.EqualTo(10)); Assert.That(hit.Duration, Is.EqualTo(4f)); Assert.That(hit.Radius, Is.EqualTo(7f));
                Assert.That(hit.PreventsRunning, Is.False); Assert.That(hit.Player, Is.EqualTo(player.Id)); }
            Assert.That(module.TakeHit(out _), Is.False); module.ResolveAfterSensing(); Assert.That(module.TakeScream(out _), Is.False);
        }
        [Test] public void CurseHooksCapAndSharperEarsUsesCurrentInsteadOfLastSeenPosition()
        {
            effects = new ActiveEffects(new[] { new ActiveEffect(HeraldController.LongerDeafness, EffectKind.Curse, 99),
                new ActiveEffect(HeraldController.WiderScream, EffectKind.Curse, 99), new ActiveEffect(HeraldController.RestlessThroat, EffectKind.Curse, 99),
                new ActiveEffect(HeraldController.SharperEars, EffectKind.Curse, 1), new ActiveEffect(HeraldController.DeafLanding, EffectKind.Curse, 1) });
            player.Position = Vector3.forward * 10f; Step(); var discovery = Scream();
            Assert.That(discovery.ExactPosition, Is.True); Assert.That(discovery.FloorWideHint.Position, Is.EqualTo(player.Position));
            Assert.That(discovery.ObservedTick, Is.EqualTo(tick)); Assert.That(module.CadenceMultiplier, Is.EqualTo(Mathf.Pow(.75f, 3)));
            Step(); Step(.8f); Assert.That(Scream().Pitch, Is.EqualTo(1f)); Assert.That(module.TakeHit(out var hit), Is.True);
            Assert.That(hit.Radius, Is.EqualTo(7f * Mathf.Pow(1.2f, 3))); Assert.That(hit.Duration, Is.EqualTo(4f * Mathf.Pow(1.5f, 3)));
            Assert.That(hit.PreventsRunning, Is.True); Assert.That(config.DeafenSeconds, Is.EqualTo(4f));
        }
        [Test] public void LostSightStopsChaseCallsButCommittedAttackStillBroadcastsStaleClue()
        {
            player.Position = Vector3.forward * 6f; Step(); Scream(); Step(); hunter.PlayerVisible = false;
            player.Position = Vector3.forward * 20f; Step(.8f); var attack = Scream();
            Assert.That(attack.FloorWideHint.Position, Is.EqualTo(hunter.LastKnownPosition)); Assert.That(attack.ExactPosition, Is.False);
            Assert.That(module.TakeHit(out _), Is.False); Step(10f); Assert.That(module.TakeScream(out _), Is.False);
        }
        [Test] public void RetreatCatchDeathAndResetCancelWarning()
        {
            player.Position = Vector3.forward; Step(); Scream(); Step(); hunter.PursuitSuppressed = true; Step(1f);
            Assert.That(module.TakeHit(out _) || module.TakeScream(out _), Is.False); Assert.That(module.Hold, Is.False);
            hunter.PursuitSuppressed = false; Step(); Scream(); Step(); Step(1f, false); Assert.That(module.TakeHit(out _), Is.False);
            player.Health = 0; Step(10f); Assert.That(module.TakeScream(out _), Is.False);
            module.Reset(Context()); Assert.That(module.TakeBreath(out _), Is.False);
        }
        [Test] public void SharedPursuitNeverStartsMeleeAndUsesFreshSensingForDiscovery()
        {
            var state = new HunterBehaviorState(); var shared = new HunterController(state, profile, new System.Random(7), player, world, module);
            player.Position = Vector3.forward; shared.Reset(hunter.Id, Vector3.zero, Vector3.forward);
            var result = shared.Tick(new SightProbe(true, true, true), .1f, 1); module.ResolveAfterSensing();
            Assert.That(result.BeginLunge, Is.False); Assert.That(result.Phase, Is.EqualTo(HunterLungePhase.None));
            Assert.That(Scream().Sound, Is.EqualTo(HeraldSound.Discovery)); Assert.That(state.PlayerVisible, Is.True);
            Assert.That(result.Speed, Is.GreaterThan(0f));
        }
        [Test] public void EveryScreamIsExcludedFromFloorWideHunterHearing()
        {
            Assert.That(HunterRegistry.Items, Is.Empty);
            var objects = new List<GameObject>(); var states = new List<HunterBehaviorState>(); var actors = new List<HunterManager>();
            var directorObject = new GameObject("Herald Director delivery test"); var director = directorObject.AddComponent<DirectorManager>();
            int deliveries = 0; director.OnNoiseHintIssued += (_, __) => deliveries++;
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    var go = new GameObject("Herald listener test"); go.SetActive(false); objects.Add(go);
                    var actor = go.AddComponent<HunterManager>(); var state = new HunterBehaviorState();
                    var shared = new HunterController(state, profile, new System.Random(7), player, world);
                    shared.Reset(new EntityId(-10 - i), Vector3.one * 10000f, Vector3.forward);
                    EchoControllerTests.Tune(actor, "_state", state); EchoControllerTests.Tune(actor, "_controller", shared);
                    go.SetActive(true); Registry("Register", actor); states.Add(state); actors.Add(actor);
                }
                void Deliver()
                {
                    var fact = Scream(); director.HearFloorWideNoise(fact.FloorWideHint); director.HearFloorWideNoise(fact.FloorWideHint);
                }
                Step(); Deliver(); Step(4f); Deliver(); Step(4f); Deliver();
                player.Position = Vector3.forward; Step(); Step(.8f); Deliver();
                Assert.That(deliveries, Is.Zero);
                foreach (var state in states)
                    Assert.That(((System.Collections.IList)typeof(HunterBehaviorState).GetField("HeardNoises", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state)).Count, Is.Zero);
                // A second Herald does not turn sound into admitted gameplay provenance.
                director.HearFloorWideNoise(new NoiseEvent(new EntityId(-2), hunter.LastKnownPosition, 1f, tick, NoiseSourceKind.Scream));
                Assert.That(deliveries, Is.Zero);
            }
            finally
            { foreach (var actor in actors) Registry("Unregister", actor); foreach (var go in objects) Object.DestroyImmediate(go); Object.DestroyImmediate(directorObject); }
        }
        private static void Registry(string method, HunterManager actor) => typeof(HunterRegistry).GetMethod(method,
            BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { actor });
    }
}
