// ============================================================================
// FloorCollapseIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Exercises generated visual pools, room clipping, pickups, and hand facts against live engine probes.
//   Explicit observations and elapsed time keep room hazards reproducible.
//   Room-local ownership prevents effects or contacts leaking across portals.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) Â· Domain Â· Floor.
// KEY RESPONSIBILITIES:
//   - Keep collapse presentation aligned with the staged gameplay hazard.
//   - Preserve one escape opportunity and exactly one hit per committed grab.
// DEPENDENCIES:
//   - Core shared floor facts and Unity value types; no higher-layer dependency.
// USAGE NOTES:
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
    public sealed class FloorCollapseIntegrationTests
    {
        [Test]
        public void StagesUsePooledHandsAndMistWithoutClosureWallsOrInstantOccupantDeath()
        {
            using (var fixture = new Fixture())
            {
                fixture.Open();
                var room = fixture.Root.GetComponentInChildren<RoomCollapseVolume>();
                int count = fixture.Root.GetComponentsInChildren<Transform>(true).Length;
                fixture.Manager.Tick(6f, 1);
                Assert.That(room.Phase, Is.EqualTo(RoomPhase.Tearing));
                Assert.That(fixture.Player.Health, Is.EqualTo(100f));
                fixture.Manager.Tick(2f, 2);
                Assert.That(room.Phase, Is.EqualTo(RoomPhase.Encroaching));
                fixture.Manager.Tick(6f, 3);
                Assert.That(room.Phase, Is.EqualTo(RoomPhase.Closed));
                Assert.That(fixture.Player.Health, Is.EqualTo(100f), "A large stage tick cannot skip hand warning and grace.");
                Assert.That(fixture.Root.GetComponentsInChildren<NavMeshObstacle>(true), Is.Empty);
                Assert.That(fixture.Root.GetComponentsInChildren<Transform>(true).Any(t => t.name.Contains("Closure Door Blocker")), Is.False);
                Assert.That(fixture.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
        }
        [Test]
        public void GoldenCakesRemainDuringCrackingAndTearingThenDisappearOnlyAtTheirMistFront()
        {
            using (var fixture = new Fixture())
            {
                fixture.Open(); fixture.Manager.Tick(6.5f, 1);
                Assert.That(fixture.Driver.PickupAvailable(101), Is.True);
                Assert.That(fixture.Driver.PickupAvailable(102), Is.True);
                fixture.Manager.Tick(3.5f, 2);
                Assert.That(fixture.Driver.PickupAvailable(102), Is.False, "Outer cake is overtaken first.");
                Assert.That(fixture.Driver.PickupAvailable(101), Is.True, "Central cake remains accessible.");
                fixture.Manager.Collect(fixture.Player.Id, 101, PickupKind.GoldenCake);
                Assert.That(fixture.Manager.ReadOnlyState.GoldenCakeCount, Is.EqualTo(1));
                fixture.Manager.Collect(fixture.Player.Id, 102, PickupKind.GoldenCake);
                Assert.That(fixture.Manager.ReadOnlyState.GoldenCakeCount, Is.EqualTo(1));
            }
        }
        [Test]
        public void RealHandProbeRoutesWarningGrabNormalHitAndLethalConsumptionOnlyAfterHealthReachesZero()
        {
            using (var fixture = new Fixture())
            {
                fixture.Open(); fixture.Manager.Tick(8f, 1);
                var room = fixture.Root.GetComponentInChildren<RoomCollapseVolume>();
                var hand = room.GetComponentsInChildren<Transform>(true).First(t => t.name == "Shadow Hand 0");
                fixture.Player.Position = hand.position;
                var facts = new List<CollapseHandEventKind>(); int deaths = 0;
                fixture.Manager.OnCollapseHand += fact =>
                {
                    facts.Add(fact.Kind);
                    if (fact.Kind == CollapseHandEventKind.Hit)
                    {
                        fixture.Player.Health -= fact.Damage;
                        if (!fixture.Player.IsAlive) fixture.Manager.ConfirmCollapseDeath(fact.PlayerId, fact.RoomId);
                    }
                };
                fixture.Manager.OnLethalContact += _ => deaths++;
                fixture.Manager.Tick(0f, 2);
                fixture.Manager.Tick(0.7f, 3);
                fixture.Manager.Tick(1.4f, 4);
                CollectionAssert.AreEqual(new[]{CollapseHandEventKind.Warning,CollapseHandEventKind.Grabbed,CollapseHandEventKind.Hit},facts);
                Assert.That(fixture.Player.Health, Is.EqualTo(75f)); Assert.That(deaths, Is.Zero);
                fixture.Player.Health = 25f;
                fixture.Manager.Tick(2f, 5); fixture.Manager.Tick(0f, 6);
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
                Assert.That(fixture.Driver.QueryHand(fixture.Origin + new Vector3(6.1f,0f,0f)).Available, Is.False);
                Assert.That(fixture.Driver.QueryHand(fixture.Origin + new Vector3(0f,7.2f,0f)).Available, Is.False);
                var hand = room.GetComponentsInChildren<Transform>(true).First(t => t.name == "Shadow Hand 0");
                Vector3 target = hand.position + Vector3.right;
                Assert.That(room.Probe(target,0).Available, Is.True);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    wall.transform.position = hand.position + new Vector3(0.5f,0.6f,0f);
                    wall.transform.localScale = new Vector3(0.2f,2f,2f);
                    Physics.SyncTransforms();
                    Assert.That(room.Probe(target,0).Available, Is.False);
                }
                finally { Object.DestroyImmediate(wall); }
            }
        }
        private sealed class Fixture : IDisposable
        {
            public readonly Vector3 Origin = new Vector3(40000f,0f,40000f);
            public readonly GameObject Root;
            public readonly FloorManager Manager;
            public readonly FloorDriver Driver;
            public readonly PlayerView Player = new PlayerView();
            private readonly FloorConfig _config;
            private readonly FloorDriverConfig _visual;
            public Fixture()
            {
                _config = ScriptableObject.CreateInstance<FloorConfig>();
                _visual = ScriptableObject.CreateInstance<FloorDriverConfig>();
                typeof(FloorConfig).GetField("_requiredCakeCount", BindingFlags.NonPublic|BindingFlags.Instance).SetValue(_config,2);
                Root = new GameObject("Collapse isolated fixture"); Root.SetActive(false);
                Driver = Root.AddComponent<FloorDriver>(); Manager = Root.AddComponent<FloorManager>();
                typeof(FloorDriver).GetField("_config",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(Driver,_visual);
                Player.Position = Origin;
                var graph = LevelGraphUtility.Build(new[]{new LevelRoom(1,Origin+Vector3.up*3.5f,new Vector3(12f,7f,12f))},
                    Array.Empty<LevelEdge>(),new[]{new LevelAnchor(101,1,CakeAnchorType.Flow,Origin),new LevelAnchor(102,1,CakeAnchorType.Flow,Origin+Vector3.right*4.4f)},1,Origin+Vector3.forward*4f);
                Root.SetActive(true); Manager.Initialize(_config,new LevelView(graph),new[]{Player},new System.Random(3));
            }
            public void Open() { Manager.Collect(Player.Id,101,PickupKind.Cake);Manager.Collect(Player.Id,102,PickupKind.Cake); }
            public void Dispose()
            { if(Manager!=null)Manager.Teardown();Object.DestroyImmediate(Root);Object.DestroyImmediate(_config);Object.DestroyImmediate(_visual); }
        }
        private sealed class LevelView : IReadOnlyLevelState
        {
            public LevelView(LevelGraph graph){Graph=graph;} public bool IsReady=>true;public LevelGraph Graph{get;}
        }
        private sealed class PlayerView : IReadOnlyPlayerState
        {
            public EntityId Id=>new EntityId(1);public Vector3 Position{get;set;}
            public Vector3 Velocity=>Vector3.zero;public Vector3 Forward=>Vector3.forward;public float HeadingDegrees=>0f;
            public float SprintSpeed=>8f;public float MaxDesignSpeed=>12f;public float Health{get;set;}=100f;
            public float MaxHealth=>100f;public bool IsAlive=>Health>0f;public bool LookBack=>false;
            public MovementState MovementState=>MovementState.Ground;public long Tick=>0;
            public IReadOnlyList<NoiseEvent> RecentNoises=>Array.Empty<NoiseEvent>();public InventorySnapshot Inventory=>default;
        }
    }
}
