// ============================================================================
// ExpeditionFollowupTests.cs
// ============================================================================
// PURPOSE:
//   Verifies Passage rewards, archetype spawn admission and identified Vault routing.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Expedition.
// KEY RESPONSIBILITIES:
//   - Exercise actual bridge success/failure before Floor collapse and optional gold.
//   - Isolate navigation area admission and preserve resolved Vault identity.
// DEPENDENCIES:
//   Core, Domain Floor/Level/Player/Procedural, Session Expedition/Run/Progression,
//   NUnit, reflection and native Unity navigation in coordinator-run Edit Mode.
// USAGE NOTES:
//   Transient remote geometry only; removes only owned navigation and objects.
//   Source guards supplement the Run identity relay and explicit destination checks.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Domain.Procedural;
using Worsen.Session.Expedition;
using Worsen.Session.Progression;
using Worsen.Session.Run;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Expedition
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ExpeditionFollowupTests
    {
        private readonly List<Object> owned = new List<Object>();
        [TearDown] public void Cleanup()
        { for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]); owned.Clear(); }
        private T Config<T>() where T : ScriptableObject
        { var value = Worsen.Tests.Core.ShaderReferenceTestSetup.Create<T>(); owned.Add(value); return value; }
        private T Component<T>() where T : Component
        { var go = new GameObject(typeof(T).Name); go.SetActive(false); owned.Add(go); return go.AddComponent<T>(); }
        private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private static string Source(string path) => File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/" + path));
        private ExpeditionSessionManager Route(ProceduralManager procedural, FloorManager floor)
        {
            var route = Component<ExpeditionSessionManager>(); var state = new ExpeditionSessionBehaviorState();
            var controller = new ExpeditionSessionController(state); controller.Bind(SceneKey.HorrorRun);
            controller.Queue(new ProgressionGenerationRequest(1, 19, 12, false, new ProgressionEffects(1, 1, 1, 1, 100, 100, 0)));
            controller.Begin(1); controller.RecordPlayer(new EntityId(1)); controller.Ready();
            Set(route, "_state", state); Set(route, "_controller", controller); Set(route, "_procedural", procedural);
            Set(route, "_floor", floor); Set(route, "_run", Component<RunSessionManager>());
            Set(route, "_progression", Component<ProgressionSessionManager>()); Call(route, "OnEnable");
            return route;
        }
        [Test]
        public void PassageUsesIdentifiedPocketOnlyAfterBridgeSuccessAndLinesItOnce()
        {
            var config = Config<ProceduralConfig>(); var visual = Config<ProceduralDriverConfig>();
            Set(config, "_origin", new Vector2(30000, 30000)); Set(config, "_castleModules", false);
            Set(config, "_storeyProbability", 0f); Set(config, "_gapProbability", 1f);
            Set(config, "_pocketProbability", 1f); Set(config, "_ordinaryDoorFraction", 1f);
            var procedural = Component<ProceduralManager>(); procedural.Initialize(config, visual, 19, 12);
            var level = Component<LevelManager>(); level.InitializeGenerated(procedural.Graph);
            var floor = Component<FloorManager>(); var floorConfig = Config<FloorConfig>();
            Set(floorConfig, "_useRoomCakeDensity", false); Set(floorConfig, "_requiredCakeCount", 1);
            Set(floor.GetComponent<FloorDriver>(), "_config", Config<FloorDriverConfig>());
            var player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100, Position = procedural.PlayerSpawnPosition };
            floor.Initialize(floorConfig, level.ReadOnlyState, new[] { player }, new System.Random(19), cakeHooks: new FloorCakeHooks(greedyDoor: true));
            var route = Route(procedural, floor);
            Call(route, "OnEnable");
            Assert.That(((Delegate)Get(procedural, "PassageOpened")).GetInvocationList().Length, Is.EqualTo(1));
            int siteIndex = procedural.ShrineSites.ToList().FindIndex(s => s.GapEdge);
            Assert.That(siteIndex, Is.GreaterThanOrEqualTo(0)); var site = procedural.ShrineSites[siteIndex];
            var driverState = (ProceduralDriverState)Get(procedural.GetComponent<ProceduralDriver>(), "_state");
            ShrineResolvedFact Fact(int id) => new ShrineResolvedFact(1,
                new ShrineActivatedFact(id, ShrineKind.Passage, site.RoomId, site.Position, id), ShrineKind.Passage, false);
            try
            {
                driverState.Ready = false;
                LogAssert.Expect(LogType.Warning, "Shrine 1: passage-pocket-unavailable");
                Call(route, "HandleShrineResolved", Fact(1));
                Assert.That(floor.ReadOnlyState.RoomPhases[site.DestinationPocketRoomId], Is.EqualTo(RoomPhase.Open));
                Assert.That(procedural.LinedPocketAnchors, Is.Empty);
                driverState.Ready = true;
                Call(route, "HandleShrineResolved", Fact(2)); Call(route, "HandleShrineResolved", Fact(2));
                ((FloorController)Get(floor, "_controller")).Tick(floorConfig.PocketCollapseDelay, 3);
                Assert.That(floor.ReadOnlyState.RoomPhases[site.DestinationPocketRoomId], Is.EqualTo(RoomPhase.Telegraph));
                foreach (var room in procedural.Graph.Rooms.Where(r => r.Pocket && r.Id != site.DestinationPocketRoomId))
                    Assert.That(floor.ReadOnlyState.RoomPhases[room.Id], Is.EqualTo(RoomPhase.Open));
                Assert.That(procedural.LinedPocketAnchors, Is.Not.Empty);
                Call(route, "HandlePassageOpened", siteIndex, site.DestinationPocketRoomId, Array.Empty<Vector3>());
                Call(route, "Unsubscribe");
                Assert.That(Get(procedural, "PassageOpened"), Is.Null);
                foreach (var anchor in procedural.LinedPocketAnchors)
                {
                    Assert.That(floor.GetComponentsInChildren<CakePickup>(true).Count(p => p.AnchorId == anchor.Id), Is.EqualTo(1));
                    int before = floor.ReadOnlyState.GoldenCakeCount;
                    floor.Collect(player.Id, anchor.Id, PickupKind.GoldenCake);
                    Assert.That(floor.ReadOnlyState.GoldenCakeCount, Is.EqualTo(before + 1));
                    Assert.That(floor.RegisterPassageReward(anchor), Is.False);
                }
                Assert.That(floor.ReadOnlyState.CakeCount, Is.Zero); Assert.That(floor.ReadOnlyState.RequiredCakeCount, Is.EqualTo(1));
                foreach (var anchor in floor.ReadOnlyState.ActiveCakeAnchors.ToArray()) floor.Collect(player.Id, anchor.Id, PickupKind.Cake);
                Assert.That(floor.ReadOnlyState.ExitState, Is.EqualTo(ExitState.Locked), "Passage gold cannot pay Greedy Door.");
                StringAssert.Contains("_procedural.ActivatePassage(siteIndex) && _floor.ActivatePocket(site.DestinationPocketRoomId)",
                    Source("Session/Expedition/Manager/ExpeditionSessionManager.cs"));
            }
            finally { Call(route, "Unsubscribe"); floor.Teardown(); procedural.Teardown(); }
        }
        [Test]
        public void SpawnValidationRejectsPartitionAreaByDefaultButAdmitsWeaverMask()
        {
            var origin = new Vector3(50000, 0, 50000); var settings = NavMesh.GetSettingsByIndex(0);
            settings.minRegionArea = 0f;
            var sources = new List<NavMeshBuildSource> { new NavMeshBuildSource {
                shape = NavMeshBuildSourceShape.Box, transform = Matrix4x4.TRS(origin - Vector3.up * .15f, Quaternion.identity, Vector3.one),
                size = new Vector3(20, .3f, 20), area = 3 } };
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(origin, new Vector3(24, 4, 24)), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null); owned.Add(data); var instance = NavMesh.AddNavMeshData(data);
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube); owned.Add(cover);
            cover.transform.position = origin + Vector3.up * 2; cover.transform.localScale = new Vector3(1, 5, 8);
            Physics.SyncTransforms(); var probe = Component<ExpeditionSpawnDriver>(); var config = Config<ExpeditionSpawnDriverConfig>();
            try
            {
                Assert.That(probe.Validate(origin + Vector3.right * 4, origin - Vector3.right * 4, config), Is.False);
                Assert.That(probe.Validate(origin + Vector3.right * 4, origin - Vector3.right * 4, config, 9), Is.True);
                var guidance = Component<FloorDriver>(); Set(guidance, "_config", Config<FloorDriverConfig>());
                Assert.That(guidance.PathLength(origin + Vector3.right * 4, origin - Vector3.right * 4), Is.EqualTo(float.PositiveInfinity));
                StringAssert.Contains("profile.MotorOverride.NavigationAreaMask", Source("Session/Expedition/Manager/ExpeditionSessionManager.cs"));
            }
            finally { if (instance.valid) instance.Remove(); }
        }
        [TestCase(true)] [TestCase(false)]
        public void VaultProbeIdentitySurvivesCompletionAndRunEventIntoPuzzle(bool succeeded)
        {
            var profile = Config<PlayerProfile>(); var state = new PlayerBehaviorState();
            var player = new PlayerController(state, profile, new System.Random(19)); player.Reset(new EntityId(1), Vector3.zero, 0f);
            var probe = new MovementProbe(true, Vector3.up, vaultCandidate: true, vaultHeight: profile.VaultMinimumHeight,
                vaultClearance: 2f, vaultTarget: Vector3.forward, surfaceId: 90);
            var frame = new InputFrame(Vector2.zero, Vector2.zero, InputButtons.Jump, InputButtons.Jump, InputButtons.None);
            player.Tick(frame, probe, .02f, 1); Assert.That(state.VaultSurfaceId, Is.EqualTo(90));
            PlayerTickResult decision = default;
            for (int i = 2; i < 60 && !state.VaultCompletionPending; i++) decision = player.Tick(default, default, .02f, i);
            Assert.That(state.VaultCompletionPending, Is.True);
            player.CommitPose(new PlayerMoveResult(succeeded ? probe.VaultTarget : Vector3.left * 10, Vector3.zero, true, false));
            player.CommitFrame(Vector3.up, default, decision.Facts);
            var fact = state.LastTraversalFacts.Single(f => f.Kind == TraversalKind.Vault);
            Assert.That(fact.SurfaceId, Is.EqualTo(90)); Assert.That(fact.Succeeded, Is.EqualTo(succeeded));
            var procedural = Component<ProceduralManager>(); var driver = procedural.GetComponent<ProceduralDriver>(); Set(procedural, "_driver", driver);
            Property(Get(procedural, "_state"), "IsReady", true);
            var driverState = (ProceduralDriverState)Get(driver, "_state"); driverState.Ready = true;
            var puzzle = Component<ProceduralPuzzleModule>();
            Property(puzzle, "Plan", new ProceduralPuzzlePlan(9, 1, ProceduralPuzzleKind.TimedVaults, Vector3.zero, true, default));
            var puzzleState = new ProceduralPuzzleDriverState { Next = 2, Started = true }; Set(puzzle, "_state", puzzleState);
            driverState.Puzzles.Add(puzzle); var route = Route(procedural, Component<FloorManager>());
            try
            {
                var run = (RunSessionManager)Get(route, "_run");
                ((Action<PlayerTraversalFact>)Get(run, "PlayerTraversalPublished"))(fact);
                if (succeeded) Assert.That(Get(puzzle, "_vault"), Is.EqualTo(0));
                else Assert.That(puzzleState.Next, Is.Zero);
                StringAssert.Contains("PlayerTraversalPublished?.Invoke(fact)", Source("Session/Run/Manager/RunSessionManager.cs"));

            }
            finally { Call(route, "Unsubscribe"); Property(Get(procedural, "_state"), "IsReady", false); driverState.Puzzles.Clear(); }
        }
        [Test]
        public void DriverCapturesAuthoredVaultSurfaceRatherThanWallIdentity()
        {
            var origin = new Vector3(60000, 0, 60000);
            var driver = Component<PlayerDriver>(); driver.transform.position = origin;
            var config = Config<PlayerMoverDriverConfig>(); Set(config, "_hunterBodyLayer", "Ignore Raycast"); Set(driver, "_config", config);
            driver.Initialize();
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); owned.Add(obstacle);
            obstacle.transform.position = origin + new Vector3(0, .3f, .8f); obstacle.transform.localScale = new Vector3(2, .6f, .2f);
            obstacle.AddComponent<ProceduralTraversalSurface>().Configure(new ProceduralBlock(1, ProceduralSurfaceKind.Wall,
                obstacle.transform.position, obstacle.transform.localScale, 90, TraversalSurfaceKind.Vault,
                origin, origin + Vector3.forward * 1.6f));
            Physics.SyncTransforms(); var probe = driver.Probe();
            Assert.That(probe.VaultCandidate, Is.True); Assert.That(probe.SurfaceId, Is.EqualTo(90));
            // Wall jumps (owner playtest): vault geometry also qualifies as a wall; vault keeps priority.
            Assert.That(probe.WallId, Is.Zero.Or.EqualTo(90));
        }
        [Test]
        public void RejectedVaultPublishesItsProbeIdentityImmediately()
        {
            var state = new PlayerBehaviorState(); var player = new PlayerController(state, Config<PlayerProfile>(), new System.Random(19));
            player.Reset(new EntityId(1), Vector3.zero, 0f);
            var result = player.Tick(new InputFrame(Vector2.zero, Vector2.zero, InputButtons.Jump, InputButtons.Jump, InputButtons.None),
                new MovementProbe(true, Vector3.up, vaultCandidate: true, vaultHeight: .5f, vaultClearance: 0f, surfaceId: 91), .02f, 1);
            var fact = result.Facts.Single(f => f.Kind == TraversalKind.Vault);
            Assert.That(fact.SurfaceId, Is.EqualTo(91)); Assert.That(fact.Succeeded, Is.False);
        }
    }
}
