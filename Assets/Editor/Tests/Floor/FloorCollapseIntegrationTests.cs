// ============================================================================
// FloorCollapseIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Exercises generated visual pools, room clipping, pickups, and hand facts against live engine probes.
//   Dedicated boundary overlaps, not visual hand positions, drive the registered actor.
//   Explicit observations and elapsed time keep room hazards reproducible.
//   Room-local ownership prevents effects or contacts leaking across portals.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) Â· Domain Â· Floor.
// KEY RESPONSIBILITIES:
//   - Keep collapse presentation aligned with the staged gameplay hazard.
//   - Preserve one escape opportunity and exactly one hit per committed grab.
//   - Verify Closed seals, cue emission, footprint placement and Low Profile release.
// DEPENDENCIES:
//   - Core shared floor facts and Unity value types; no higher-layer dependency.
// USAGE NOTES:
//   ShaderReferenceTestSetup explicitly binds shaders for transient generated visuals.
//   The two fixed hazard sockets explicitly use legacy authored-count mode.
//   Scene-owned through FloorManager/FloorDriver. Time is supplied by the owner.
//   No persistent singleton, global settings, or independent update loop.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Floor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000)]
    public sealed class FloorCollapseIntegrationTests
    {
        [Test]
        public void StagesUsePooledFrontAndEnableClosedSealsWithoutInstantOccupantDeath()
        {
            using (var fixture = new Fixture())
            {
                fixture.Open();
                var room = fixture.Root.GetComponentInChildren<RoomCollapseVolume>();
                int count = fixture.Root.GetComponentsInChildren<Transform>(true).Length;
                var cues = new List<CueId>(); fixture.Driver.CollapseCue += (cue, position, roomId) => cues.Add(cue);
                fixture.Manager.Tick(4f, 1);
                Assert.That(room.Phase, Is.EqualTo(RoomPhase.Tearing));
                Assert.That(fixture.Player.Health, Is.EqualTo(100f));
                fixture.Manager.Tick(2f, 2);
                Assert.That(room.Phase, Is.EqualTo(RoomPhase.Encroaching));
                fixture.Manager.Tick(4f, 3);
                Assert.That(room.Phase, Is.EqualTo(RoomPhase.Closed));
                Assert.That(fixture.Player.Health, Is.EqualTo(100f), "A large stage tick cannot skip hand warning and grace.");
                Assert.That(fixture.Root.GetComponentsInChildren<NavMeshObstacle>(true), Is.Empty);
                Assert.That(room.GetComponentsInChildren<BoxCollider>(true).Count(c => c.name == "Closed fog seal" && c.enabled && !c.isTrigger), Is.EqualTo(4));
                Assert.That(cues, Is.EqualTo(new[] { CueId.RoomTear, CueId.MistAdvance, CueId.RoomConsumed }));
                Assert.That(fixture.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
        }
        [Test]
        public void CompletedCollectionNeverRespawnsGoldOrPublishesCakeLoss()
        {
            using (var fixture = new Fixture())
            {
                fixture.Open(); fixture.Manager.Tick(6.5f, 1);
                Assert.That(fixture.Driver.PickupAvailable(101), Is.False);
                Assert.That(fixture.Driver.PickupAvailable(102), Is.False);
                var losses = new List<int>();
                fixture.Manager.OnCakeLost += (anchor, roomId, kind, tick) => losses.Add(anchor);
                fixture.Manager.Tick(3.5f, 2);
                Assert.That(fixture.Driver.PickupAvailable(102), Is.False);
                Assert.That(fixture.Driver.PickupAvailable(101), Is.False);
                fixture.Manager.Collect(fixture.Player.Id, 101, PickupKind.GoldenCake);
                Assert.That(fixture.Manager.ReadOnlyState.GoldenCakeCount, Is.Zero);
                fixture.Manager.Tick(4f, 3);
                fixture.Manager.Collect(fixture.Player.Id, 102, PickupKind.GoldenCake);
                Assert.That(fixture.Manager.ReadOnlyState.GoldenCakeCount, Is.Zero);
                Assert.That(losses, Is.Empty);
                Assert.That(fixture.Root.GetComponentsInChildren<CakePickup>(true)
                    .Any(value => value.Kind == PickupKind.GoldenCake), Is.False);
            }
        }
        [Test]
        public void RealHandProbeRoutesWarningGrabNormalHitAndLethalConsumptionOnlyAfterHealthReachesZero()
        {
            using (var fixture = new Fixture())
            {
                fixture.Open(); fixture.Manager.Tick(10f, 1);
                var room = fixture.Root.GetComponentInChildren<RoomCollapseVolume>();
                Assert.That(room.GetComponent<BoxCollider>().isTrigger, Is.True);
                Assert.That(room.GetComponent<BoxCollider>().enabled, Is.True);
                fixture.Move(fixture.Origin + Vector3.right * 6.25f);
                var facts = new List<CollapseHandEventKind>(); int deaths = 0;
                var cues = new List<CueId>(); fixture.Driver.CollapseCue += (cue, point, roomId) => cues.Add(cue);
                int noises = 0;
                fixture.Manager.OnHandNoise += noise => { noises++; Assert.That(noise.Loudness, Is.GreaterThan(0f)); };
                fixture.Manager.OnCollapseHand += fact =>
                {
                    facts.Add(fact.Kind);
                    if (fact.Kind == CollapseHandEventKind.Hit)
                    {
                        Assert.That(fact.ThrowVelocity, Is.EqualTo(Vector3.right * 8f));
                        fixture.Player.Health -= fact.Damage;
                        if (!fixture.Player.IsAlive) fixture.Manager.ConfirmCollapseDeath(fact.PlayerId, fact.RoomId);
                    }
                };
                fixture.Manager.OnLethalContact += _ => deaths++;
                fixture.Manager.Tick(0f, 2);
                fixture.Manager.Tick(0.7f, 3);
                fixture.Manager.Tick(1.4f, 4);
                CollectionAssert.AreEqual(new[]{CollapseHandEventKind.Warning,CollapseHandEventKind.Grabbed,CollapseHandEventKind.Hit},facts);
                Assert.That(cues, Is.EqualTo(new[] { CueId.GrabWarning, CueId.GrabStart, CueId.GrabHit }));
                Assert.That(fixture.Player.Health, Is.EqualTo(75f)); Assert.That(deaths, Is.Zero);
                Assert.That(noises, Is.EqualTo(1));
                Assert.That(fixture.Manager.ReadOnlyState.RoomHandPhases[1], Is.EqualTo(FloorHandPhase.Cooldown));
                Assert.That(fixture.Manager.ReadOnlyState.RoomPhases[2], Is.EqualTo(RoomPhase.Open));
                fixture.Player.Health = 25f;
                fixture.Manager.Tick(2.4f, 5); fixture.Manager.Tick(0f, 6);
                fixture.Manager.Tick(0.7f, 7); fixture.Manager.Tick(1.4f, 8);
                Assert.That(facts.Last(), Is.EqualTo(CollapseHandEventKind.Consumed));
                Assert.That(deaths, Is.EqualTo(1));
                fixture.Manager.ContactLethalRoom(fixture.Player.Id, 1);
                Assert.That(deaths, Is.EqualTo(1));
            }
        }
        [Test]
        public void PortalBoundaryAndOpaqueWallPreventHandReach()
        {
            using (var fixture = new Fixture())
            {
                fixture.Open(); fixture.Manager.Tick(14f, 1);
                var room = fixture.Root.GetComponentInChildren<RoomCollapseVolume>();
                Assert.That(fixture.Driver.QueryHand(fixture.Origin + new Vector3(6.1f,0f,0f)).Available, Is.True);
                Assert.That(fixture.Driver.QueryHand(fixture.Origin + new Vector3(0f,7.2f,0f)).Available, Is.False);
                Vector3 target = fixture.Origin + Vector3.right * 6.8f;
                Assert.That(room.Probe(target,1).Available, Is.True);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    wall.transform.position = fixture.Origin + new Vector3(6.4f,0.6f,0f);
                    wall.transform.localScale = new Vector3(0.2f,2f,2f);
                    Physics.SyncTransforms();
                    Assert.That(room.Probe(target,1).Available, Is.False);
                }
                finally { Object.DestroyImmediate(wall); }
            }
        }
        [Test]
        public void BoundaryImpulsePublishesOnlyOnEntryAndReentryAndDisabledActorReleasesGrab()
        {
            using (var fixture = new Fixture())
            {
                fixture.Open(); fixture.Manager.Tick(14f, 1);
                fixture.Move(fixture.Origin + Vector3.right * 5.8f);
                var impulses = new List<FloorBoundaryImpulseFact>();
                var facts = new List<CollapseHandEventKind>();
                fixture.Manager.OnBoundaryImpulse += impulses.Add;
                fixture.Manager.OnCollapseHand += fact => facts.Add(fact.Kind);
                fixture.Manager.Tick(0f, 2);
                fixture.Manager.Tick(0.7f, 3);
                Assert.That(impulses.Count, Is.EqualTo(1), "Continued contact is not acceleration.");
                Assert.That(impulses[0].Velocity.magnitude, Is.EqualTo(FloorCollapseHazardConfig.DefaultBounceSpeed));
                fixture.Actor.GetComponent<Collider>().enabled = false;
                fixture.Manager.Tick(0.1f, 4);
                Assert.That(facts.Last(), Is.EqualTo(CollapseHandEventKind.Escaped));
                fixture.Actor.GetComponent<Collider>().enabled = true;
                fixture.Move(fixture.Origin + Vector3.right * 8f); fixture.Manager.Tick(0f, 5);
                fixture.Move(fixture.Origin + Vector3.right * 5.8f); fixture.Manager.Tick(0f, 6);
                Assert.That(impulses.Count, Is.EqualTo(2));
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void ManagerPublishesLowProfileReleaseAndRejectsProtectedReacquisition(bool grabbed)
        {
            using (var fixture = new Fixture())
            {
                fixture.Open(); fixture.Manager.Tick(10f, 1);
                fixture.Move(fixture.Origin + Vector3.right * 5.8f);
                var facts = new List<CollapseHandFact>();
                fixture.Manager.OnCollapseHand += facts.Add;
                fixture.Player.IsUngrabbable = true;
                fixture.Manager.Tick(0f, 2);
                Assert.That(facts, Is.Empty);
                fixture.Player.IsUngrabbable = false;
                fixture.Manager.Tick(0f, 3);
                if (grabbed) fixture.Manager.Tick(.7f, 4);
                fixture.Player.IsUngrabbable = true;
                fixture.Manager.Tick(2f, 5);
                Assert.That(facts.Last().Kind, Is.EqualTo(CollapseHandEventKind.Released));
                Assert.That(facts.Last().SlowMultiplier, Is.EqualTo(1f));
                int count = facts.Count;
                fixture.Manager.Tick(20f, 6);
                Assert.That(facts.Count, Is.EqualTo(count));
            }
        }

        [Test]
        public void LShapeBuildsNoHandsOrVolumesInNotchAndCannotProbeIt()
        {
            using (var fixture = new Fixture())
            {
                var cells = new[] { new Bounds(fixture.Origin + Vector3.up * 3.5f, new Vector3(12f, 7f, 12f)),
                    new Bounds(fixture.Origin + new Vector3(12f, 3.5f, 0f), new Vector3(12f, 7f, 12f)),
                    new Bounds(fixture.Origin + new Vector3(0f, 3.5f, 12f), new Vector3(12f, 7f, 12f)) };
                var room = new LevelRoom(1, fixture.Origin + new Vector3(6f, 3.5f, 6f), new Vector3(24f, 7f, 24f), cells);
                var graph = LevelGraphUtility.Build(new[] { room }, Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 1, fixture.Origin);
                fixture.Manager.Teardown();
                fixture.Driver.Initialize(graph, Array.Empty<LevelAnchor>(), boundaryReach: 2.1f);
                fixture.Driver.ApplyRoomPhase(1, RoomPhase.Closed);
                var volume = fixture.Root.GetComponentInChildren<RoomCollapseVolume>();
                Vector3 notch = fixture.Origin + new Vector3(6.1f, .5f, 6.1f);
                Physics.SyncTransforms();
                Assert.That(volume.GetComponents<BoxCollider>().Length, Is.EqualTo(3));
                foreach (var collider in volume.GetComponents<BoxCollider>()) Assert.That(collider.bounds.Contains(notch), Is.False);
                var hands = volume.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Shadow Hand ")).ToArray();
                Assert.That(hands, Is.Not.Empty);
                var cakes = new[] { fixture.Origin + new Vector3(0f, 0f, 12f), fixture.Origin + new Vector3(12f, 0f, 0f) };
                foreach (float progress in new[] { 0f, .5f, 1f })
                {
                    volume.ApplyDestruction(new RoomDestructionSample(1, RoomPhase.Encroaching, progress), progress, cakes);
                    foreach (var hand in hands) Assert.That(room.ContainsXZ(hand.position), Is.True);
                }
                fixture.Driver.ApplyRoomPhase(1, RoomPhase.Closed);
                Assert.That(volume.Probe(notch).Available, Is.False);
                Assert.That(volume.PickupOvertaken(notch), Is.False);
                Assert.That(volume.PickupOvertaken(fixture.Origin), Is.True);
            }
        }

        private sealed class Fixture : IDisposable
        {
            public readonly Vector3 Origin = new Vector3(40000f,0f,40000f);
            public readonly GameObject Root;
            public readonly FloorManager Manager;
            public readonly FloorDriver Driver;
            public readonly PlayerView Player = new PlayerView();
            public readonly GameObject Actor;
            private readonly FloorConfig _config;
            private readonly FloorDriverConfig _visual;
            public Fixture()
            {
                _config = ScriptableObject.CreateInstance<FloorConfig>();
                _visual = Worsen.Tests.Core.ShaderReferenceTestSetup.Create<FloorDriverConfig>();
                typeof(FloorConfig).GetField("_requiredCakeCount", BindingFlags.NonPublic|BindingFlags.Instance).SetValue(_config,2);
                typeof(FloorConfig).GetField("_useRoomCakeDensity", BindingFlags.NonPublic|BindingFlags.Instance).SetValue(_config,false);
                Root = new GameObject("Collapse isolated fixture"); Root.SetActive(false);
                Driver = Root.AddComponent<FloorDriver>(); Manager = Root.AddComponent<FloorManager>();
                typeof(FloorDriver).GetField("_config",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(Driver,_visual);
                Player.Position = Origin;
                Actor = new GameObject("Registered boundary player"); Actor.transform.position = Origin;
                Actor.AddComponent<FloorLifecycleEntityHandle>(); Actor.AddComponent<CapsuleCollider>();
                Actor.AddComponent<Rigidbody>().isKinematic = true;
                var graph = LevelGraphUtility.Build(new[]{new LevelRoom(1,Origin+Vector3.up*3.5f,new Vector3(12f,7f,12f)),
                    new LevelRoom(2, Origin + new Vector3(12f,3.5f,0f),new Vector3(12f,7f,12f))},
                    new[]{new LevelEdge(1,1,2,true)},new[]{new LevelAnchor(101,1,CakeAnchorType.Flow,Origin),new LevelAnchor(102,1,CakeAnchorType.Flow,Origin+Vector3.right*4.4f)},2,Origin+Vector3.right*12f);
                Root.SetActive(true); Manager.Initialize(_config,new LevelView(graph),new[]{Player},new System.Random(3));
            }
            public void Move(Vector3 position) { Player.Position = position; Actor.transform.position = position; Physics.SyncTransforms(); }
            public void Open()
            { Manager.Collect(Player.Id,101,PickupKind.Cake);Manager.Collect(Player.Id,102,PickupKind.Cake);Move(Origin+Vector3.right*12f); }
            public void Dispose()
            { if(Manager!=null)Manager.Teardown();Object.DestroyImmediate(Actor);Object.DestroyImmediate(Root);Object.DestroyImmediate(_config);Object.DestroyImmediate(_visual); }
        }
        private sealed class LevelView : IReadOnlyLevelState
        {
            public LevelView(LevelGraph graph){Graph=graph;} public bool IsReady=>true;public LevelGraph Graph{get;}
        }
        private sealed class PlayerView : IReadOnlyPlayerState, IReadOnlyPlayerEffectState
        {
            public bool IsUngrabbable { get; set; }
            public EntityId Id=>new EntityId(1);public Vector3 Position{get;set;}
            public Vector3 Velocity=>Vector3.zero;public Vector3 Forward=>Vector3.forward;public float HeadingDegrees=>0f;
            public float SprintSpeed=>8f;public float MaxDesignSpeed=>12f;public float Health{get;set;}=100f;
            public float MaxHealth=>100f;public bool IsAlive=>Health>0f;public bool LookBack=>false;
            public MovementState MovementState=>MovementState.Ground;public long Tick=>0;
            public IReadOnlyList<NoiseEvent> RecentNoises=>Array.Empty<NoiseEvent>();public InventorySnapshot Inventory=>default;
        }
    }
}
