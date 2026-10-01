// ============================================================================
// FloorCakeIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Checks typed guidance and trap facts at the real Floor Manager boundary.
//   Generated cake objects are inspected separately from the pure rules and clocks.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Floor.
// KEY RESPONSIBILITIES:
//   - Verify guidance during a confirmed chase, trap contacts/noise and delayed exits.
//   - Verify nameless tiers, candle light/flicker, layered glow and unchanged triggers.
// DEPENDENCIES:
//   NUnit, UnityEngine, Core, Floor, read-only Level/Hunter and pure Chase rules.
// USAGE NOTES:
//   ShaderReferenceTestSetup explicitly binds shaders for transient generated visuals.
//   Unity Edit Mode only; temporary objects are destroyed without saving assets.
//   Contact callbacks are invoked explicitly; physical collision/rendering needs live QA.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Chase;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Floor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000)]
    public sealed class FloorCakeIntegrationTests
    {
        [Test]
        public void ManagerPublishesTypedWhiteGuidanceDuringConfirmedChaseAndNeverTargetsTraps()
        {
            using (var f = new Fixture())
            {
                IReadOnlyList<GuidanceTarget> targets = null; int publications = 0;
                f.Manager.OnGuidanceChanged += value => { targets = value; publications++; };
                f.Initialize();
                var chaseConfig = ScriptableObject.CreateInstance<ChaseConfig>();
                try
                {
                    var chaseState = new ChaseBehaviorState();
                    var chase = new ChaseController(chaseState, chaseConfig, new FloorCakeRulesTests.Player());
                    Assert.That(chase.Tick(new[] { new Hunter() }, 0.5f, 1).Started, Is.True);
                    f.Manager.Tick(0.5f, 1);
                    Assert.That(chaseState.HasActiveChase, Is.True); Assert.That(publications, Is.GreaterThan(1));
                    var white = targets.Single(t => t.Kind == GuidanceKind.WhiteArrow);
                    Assert.That(f.Manager.ReadOnlyState.ActiveCakeAnchors.Any(a => a.Id == white.AnchorId), Is.True);
                    Assert.That(f.Root.GetComponentsInChildren<FloorCakeTrap>().Any(t => t.TrapId == white.AnchorId), Is.False);
                    Assert.That(white.EntityId, Is.EqualTo(EntityId.None));
                }
                finally { Object.DestroyImmediate(chaseConfig); }
            }
        }

        [Test]
        public void RealTrapTriggerPublishesOneFactAndAnnounceNoiseAndReinitializesCleanly()
        {
            int seed = Enumerable.Range(0, 30).First(i => FloorCakeRulesTests.Start(seed: i).State.Traps.Any(t => t.Kind == FloorTrapKind.Announce));
            int id = FloorCakeRulesTests.Start(seed: seed).State.Traps.First(t => t.Kind == FloorTrapKind.Announce).Anchor.Id;
            using (var f = new Fixture())
            {
                int facts = 0, noises = 0, pickups = 0; FloorTrapSprungFact last = default;
                f.Manager.OnTrapSprung += value => { facts++; last = value; };
                f.Manager.OnTrapNoise += value => { noises++; Assert.That(value.SourceKind, Is.EqualTo(NoiseSourceKind.Trap)); };
                f.Manager.OnPickupCollected += _ => pickups++;
                f.Initialize(seed);
                var colliderRoot = new GameObject("Trap contact player");
                try
                {
                    colliderRoot.AddComponent<FloorCakeTestHandle>(); var collider = colliderRoot.AddComponent<SphereCollider>();
                    var trap = f.Root.GetComponentsInChildren<FloorCakeTrap>().Single(t => t.TrapId == id);
                    Invoke(trap, "OnTriggerEnter", collider); Invoke(trap, "OnTriggerStay", collider);
                    Assert.That(facts, Is.EqualTo(1)); Assert.That(noises, Is.EqualTo(1)); Assert.That(pickups, Is.Zero);
                    Assert.That(last.TrapId, Is.EqualTo(id)); Assert.That(last.Kind, Is.EqualTo(FloorTrapKind.Announce));
                    Assert.That(trap.gameObject.activeSelf, Is.False); Assert.That(f.Manager.ReadOnlyState.CakeCount, Is.Zero);
                    f.Initialize(seed); Assert.That(trap == null, Is.True);
                    trap = f.Root.GetComponentsInChildren<FloorCakeTrap>().Single(t => t.TrapId == id);
                    Invoke(trap, "OnTriggerEnter", collider); Assert.That(facts, Is.EqualTo(2)); Assert.That(noises, Is.EqualTo(2));
                }
                finally { Object.DestroyImmediate(colliderRoot); }
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void BlinderTellPublishesMatchingNoiseUnlessSilent(bool silent)
        {
            using (var f = new Fixture())
            {
                int noises = 0; f.Manager.OnTrapNoise += n => { noises++; Assert.That(n.Source, Is.EqualTo(EntityId.None)); };
                f.Initialize(hooks: new FloorCakeHooks(moreTraps: true, silentTraps: silent));
                int blind = f.Root.GetComponentsInChildren<FloorCakeTrap>().Count(t => t.GetComponent<AudioSource>() != null);
                Assert.That(blind, Is.GreaterThanOrEqualTo(2));
                f.Manager.Tick(2f, 1); Assert.That(noises, Is.EqualTo(silent ? 0 : blind));
            }
        }

        [Test]
        public void GreedyDoorSpawnsGoldBeforeUnlockAndPublishesGoldenSenseAlongsideWhite()
        {
            using (var f = new Fixture())
            {
                int opened = 0; IReadOnlyList<GuidanceTarget> targets = null;
                f.Manager.OnExitOpened += _ => opened++; f.Manager.OnGuidanceChanged += value => targets = value;
                f.Initialize(hooks: new FloorCakeHooks(greedyDoor: true, goldenSense: true));
                foreach (var a in f.Manager.ReadOnlyState.ActiveCakeAnchors.ToArray()) f.Manager.Collect(new EntityId(1), a.Id, PickupKind.Cake);
                Assert.That(opened, Is.Zero); Assert.That(f.Manager.ReadOnlyState.ExitState, Is.EqualTo(ExitState.Locked));
                Assert.That(f.Root.GetComponentsInChildren<CakePickup>().Count(p => p.Kind == PickupKind.GoldenCake), Is.EqualTo(6));
                Assert.That(targets.Select(t => t.Kind), Is.EqualTo(new[] { GuidanceKind.WhiteArrow, GuidanceKind.GoldenSense }));
                f.Manager.Tick(10000f, 2);
                Assert.That(opened, Is.EqualTo(1)); Assert.That(f.Manager.ReadOnlyState.ExitState, Is.EqualTo(ExitState.Open));
                f.Manager.Tick(1f, 3); Assert.That(opened, Is.EqualTo(1));
            }
        }

        [Test]
        public void BuiltCakeHasNoNameButKeepsTiersLightDeterministicFlickerAndOriginalCollider()
        {
            using (var f = new Fixture())
            {
                f.Initialize();
                var cake = f.Root.GetComponentsInChildren<CakePickup>().First();
                var trigger = cake.GetComponent<SphereCollider>();
                Assert.That(trigger.radius, Is.EqualTo(f.VisualConfig.PickupRadius)); Assert.That(trigger.isTrigger, Is.True);
                Assert.That(cake.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(cake.GetComponentsInChildren<Collider>(), Has.Length.EqualTo(1));
                Assert.That(cake.transform.Find("Baked Cake/Lower Tier"), Is.Not.Null);
                Assert.That(cake.transform.Find("Baked Cake/Frosting Rim"), Is.Not.Null);
                Assert.That(cake.GetComponentInChildren<TextMesh>(), Is.Null);
                Assert.That(typeof(FloorDriverConfig).GetProperty("PipedName"), Is.Null);
                Assert.That(typeof(FloorDriverConfig).GetField("_pipedName", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
                Assert.That(cake.GetComponentsInChildren<FloorLumenGlow>(), Has.Length.EqualTo(3));
                var light = cake.GetComponentInChildren<Light>(); Assert.That(light.type, Is.EqualTo(LightType.Point));
                f.Driver.TickCakeVisuals(0.03f); float intensity = light.intensity;
                f.Driver.TickCakeVisuals(0.09f); Assert.That(light.intensity, Is.Not.EqualTo(intensity));
                f.Driver.TickCakeVisuals(0.03f); Assert.That(light.intensity, Is.EqualTo(intensity));
                var trap = f.Root.GetComponentsInChildren<FloorCakeTrap>().First();
                Assert.That(trap.transform.Find("Baked Cake/Flame").gameObject.activeSelf, Is.False);
                Assert.That(trap.GetComponent<SphereCollider>().radius, Is.EqualTo(trigger.radius));
                foreach (var a in f.Manager.ReadOnlyState.ActiveCakeAnchors.ToArray()) f.Manager.Collect(new EntityId(1), a.Id, PickupKind.Cake);
                var gold = f.Root.GetComponentsInChildren<CakePickup>().First(p => p.Kind == PickupKind.GoldenCake);
                Assert.That(gold.transform.Find("Baked Cake/Lower Tier").GetComponent<Renderer>().sharedMaterial.color, Is.EqualTo(f.VisualConfig.GoldenColor));
                Assert.That(gold.GetComponentInChildren<Light>(), Is.Not.Null);
            }
        }

        private static void Invoke(object target, string method, object arg) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, new[] { arg });
        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root = new GameObject("Cake integration fixture");
            public readonly FloorConfig Config = ScriptableObject.CreateInstance<FloorConfig>();
            public readonly FloorDriverConfig VisualConfig = Worsen.Tests.Core.ShaderReferenceTestSetup.Create<FloorDriverConfig>();
            public readonly FloorManager Manager;
            public readonly FloorDriver Driver;
            public Fixture()
            {
                FloorCakeRulesTests.Set(Config, "_useRoomCakeDensity", false); FloorCakeRulesTests.Set(Config, "_requiredCakeCount", 6);
                FloorCakeRulesTests.Set(Config, "_flowWeight", 1f);
                Driver = Root.AddComponent<FloorDriver>(); FloorCakeRulesTests.Set(Driver, "_config", VisualConfig);
                Manager = Root.AddComponent<FloorManager>();
            }
            public void Initialize(int seed = 7, FloorCakeHooks hooks = default)
                => Manager.Initialize(Config, new Level(), new[] { new FloorCakeRulesTests.Player() }, new System.Random(seed), round: 3, cakeHooks: hooks);
            public void Dispose() { Manager.Teardown(); Object.DestroyImmediate(Root); Object.DestroyImmediate(Config); Object.DestroyImmediate(VisualConfig); }
        }
        private sealed class Level : IReadOnlyLevelState { public bool IsReady => true; public LevelGraph Graph => FloorCakeRulesTests.Graph(); }
        private sealed class Hunter : IReadOnlyHunterState
        {
            public EntityId Id => new EntityId(2); public EntityId TargetId => new EntityId(1);
            public Vector3 Position => new Vector3(60f, 0f, 3f); public Vector3 Velocity => Vector3.zero; public Vector3 Forward => Vector3.back;
            public bool PlayerVisible => true; public Vector3 LastKnownPosition => Vector3.zero;
            public long LastKnownTick => 0; public float BeliefConfidence => 1f; public long Tick => 1; public bool IsActive => true;
        }
    }
    public sealed class FloorCakeTestHandle : MonoBehaviour, IEntityHandle { public EntityId Id => new EntityId(1); }
}
