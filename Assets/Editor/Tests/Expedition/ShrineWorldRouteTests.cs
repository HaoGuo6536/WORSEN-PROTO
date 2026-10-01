// ============================================================================
// ShrineWorldRouteTests.cs
// ============================================================================
// PURPOSE:
//   Exercises producer-site assembly and real shrine world-effect routes.
//   A small isolated navigation floor admits a late hunter without regeneration.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Expedition.
// KEY RESPONSIBILITIES:
//   - Verify Passage, Pacification and one-shot Purgatory spawn/mutation routing.
//   - Build a real admitted Passage before testing its Expedition activation route.
// DEPENDENCIES:
//   - Expedition, Domain services, Core, NUnit, Unity navigation and test reflection.
// USAGE NOTES:
//   Coordinator-only Edit Mode execution under its lease. Temporary NavMesh data
//   is removed in teardown; no project scene, prefab or asset is written.
//   Registry resets explicitly remove stale Edit Mode registrations before and after each test.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Domain.Procedural;
using Worsen.Domain.Shrine;
using Worsen.Session.Expedition;
using Worsen.Session.Progression;
using Worsen.Session.Run;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Expedition
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ShrineWorldRouteTests
    {
        private readonly List<Object> owned = new List<Object>();
        private ExpeditionSessionManager expedition;
        private ProceduralManager procedural;
        private FloorManager floor;
        private FloorBehaviorState floorState;
        private PlayerManager player;
        private HunterFactory factory;
        private HunterProfile profile;
        private NavMeshDataInstance navigation;
        [SetUp]
        public void SetUp()
        {
            ResetRegistries();
            Assert.That(PlayerRegistry.Items, Is.Empty); Assert.That(HunterRegistry.Items, Is.Empty);
            player = Component<PlayerManager>();
            var mover = Config<PlayerMoverDriverConfig>(); Set(mover, "_hunterBodyLayer", "Ignore Raycast");
            Set(player.GetComponent<PlayerDriver>(), "_config", mover);
            player.Initialize(Config<PlayerProfile>(), new EntityContext(new EntityId(701), new System.Random(3)));
            typeof(PlayerRegistry).GetMethod("Register", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { player });
            var graph = new LevelGraph(new[] {
                new LevelRoom(1, new Vector3(0, 2, 0), new Vector3(16, 4, 16)),
                new LevelRoom(2, new Vector3(16, 2, 0), new Vector3(16, 4, 16)),
                new LevelRoom(3, new Vector3(0, 2, 24), new Vector3(16, 4, 16), pocket: true) },
                new[] { new LevelEdge(1, 1, 2, true) },
                new[] { new LevelAnchor(1, 1, CakeAnchorType.Flow, Vector3.zero) }, 1, Vector3.zero);
            var level = Component<LevelManager>(); level.InitializeGenerated(graph, Array.Empty<InteractableState>());
            procedural = Component<ProceduralManager>();
            var layout = new ProceduralLayout(); Property(layout, "Graph", graph);
            Property(layout, "PlayerSpawnPosition", Vector3.zero); Property(layout, "MinimumHunterSpawnRooms", 1);
            Property(layout, "HunterSpawnPositions", new[] { new Vector3(16, 0, 0) });
            Property(layout, "Doors", Array.Empty<ProceduralDoorPlan>());
            Property(layout, "ShrineSites", new[] { new ProceduralShrineSite(1, Vector3.zero, true, Vector3.forward) });
            var proceduralState = (ProceduralBehaviorState)Get(procedural, "_state");
            Property(proceduralState, "Layout", layout); Property(proceduralState, "IsReady", true);
            Set(procedural, "_config", Config<ProceduralConfig>());
            floor = Component<FloorManager>(); floorState = new FloorBehaviorState();
            var floorController = new FloorController(floorState, Config<FloorConfig>(), new System.Random(3));
            floorController.Initialize(graph, new[] { player.ReadOnlyState }, requiredCakeCount: 1);
            Set(floor, "_state", floorState); Set(floor, "_controller", floorController);
            var run = Component<RunSessionManager>(); run.BindGameplay(null, floor, null);
            var progression = Component<ProgressionSessionManager>();
            var progressionController = new ProgressionSessionController(new ProgressionSessionBehaviorState(), Config<ProgressionConfig>(), new System.Random(3));
            progressionController.StartRun(17); Set(progression, "controller", progressionController);
            var state = new ExpeditionSessionBehaviorState(); var controller = new ExpeditionSessionController(state);
            controller.Bind(SceneKey.HorrorRun);
            controller.Queue(new ProgressionGenerationRequest(1, 17, 8, false,
                new ProgressionEffects(1, 1, 1, 1, 100, 100, 1, activeThreatIds: new[] { "Hunter" })));
            controller.Begin(1); controller.RecordPlayer(player.Id);
            controller.HunterSpawns("Hunter", new[] { new Vector3(16, 0, 0) });
            controller.RecordHunter(new EntityId(-700)); controller.Ready();
            expedition = Component<ExpeditionSessionManager>();
            Set(expedition, "_state", state); Set(expedition, "_controller", controller);
            Set(expedition, "_level", level); Set(expedition, "_procedural", procedural); Set(expedition, "_floor", floor);
            Set(expedition, "_run", run); Set(expedition, "_progression", progression);
            Set(expedition, "_shrineConfig", Config<ShrineConfig>()); Set(expedition, "_shrineDriverConfig", Config<ShrineDriverConfig>());
            Set(expedition, "_spawnConfig", Config<ExpeditionSpawnDriverConfig>());
            var prefab = Component<HunterManager>();
            Set(prefab.GetComponent<HunterDriver>(), "_config", Config<HunterMotorDriverConfig>());
            profile = Config<HunterProfile>(); Set(profile, "_prefab", prefab.gameObject);
            factory = Component<HunterFactory>(); factory.Configure(profile, new System.Random(3), player.ReadOnlyState, level.ReadOnlyState);
            Set(expedition, "_hunterFactory", factory); Set(expedition, "_hunterProfile", profile);
            Call(expedition, "AssembleShrines");
        }
        [TearDown]
        public void TearDown()
        {
            // Factory's runtime Despawn deliberately uses deferred Destroy. Remove the
            // owned test actors synchronously first so Edit Mode never invokes it.
            if (factory != null)
            {
                var spawned = (IDictionary)Get(factory, "_spawned");
                foreach (HunterManager hunter in spawned.Values) { hunter.Teardown(); Object.DestroyImmediate(hunter.gameObject); }
                spawned.Clear();
            }
            if (player != null) player.Teardown();
            if (navigation.valid) navigation.Remove();
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
            ResetRegistries();
        }
        [Test]
        public void ProducerSitesAssemblePassageActivatesPocketAndReleaseRemovesShrines()
        {
            // PLAN-025 §3 C7 / §8 requires a real bridge to an identified pocket.
            // The shared synthetic setup has neither a destination nor built geometry;
            // setting IsReady alone is not admission and cannot exercise this route.
            var config = Config<ProceduralConfig>();
            var visual = Worsen.Tests.Core.ShaderReferenceTestSetup.Create<ProceduralDriverConfig>(); owned.Add(visual);
            Set(config, "_origin", new Vector2(30000f, 30000f));
            Set(config, "_castleModules", false); Set(config, "_storeyProbability", 0f);
            Set(config, "_gapProbability", 1f); Set(config, "_pocketProbability", 1f); Set(config, "_ordinaryDoorFraction", 1f);
            var unresolved = new List<string>();
            Action<ShrineResolvedFact, string> onUnresolved = (_, reason) => unresolved.Add(reason);
            ShrineManager shrines = null;
            Action<ShrineActivatedFact> onActivated = activation => Call(expedition, "HandleShrineResolved",
                new ShrineResolvedFact(1, activation, ShrineKind.Passage, false));
            expedition.ShrineWorldEffectUnresolved += onUnresolved;
            try
            {
                procedural.gameObject.SetActive(true);
                procedural.Initialize(config, visual, 19, 12);
                Assert.That(procedural.IsReady, Is.True);
                var site = procedural.ShrineSites.First(s => s.GapEdge);
                Property(player.ReadOnlyState, "Position", procedural.PlayerSpawnPosition);
                ((LevelManager)Get(expedition, "_level")).InitializeGenerated(procedural.Graph, procedural.Interactables);
                ((FloorController)Get(floor, "_controller")).Initialize(procedural.Graph, new[] { player.ReadOnlyState }, requiredCakeCount: 1);
                var previous = (ShrineManager)Get(expedition, "_shrines");
                previous.Teardown(); Object.DestroyImmediate(previous.gameObject); Set(expedition, "_shrines", null);
                Object.DestroyImmediate((ExpeditionSpawnDriver)Get(expedition, "_spawnDriver")); Set(expedition, "_spawnDriver", null);
                Set(Get(expedition, "_shrineConfig"), "_availability", new[] { new ShrineAvailability(ShrineKind.Passage, 8, FearAxis.Time) });
                Call(expedition, "AssembleShrines");
                shrines = (ShrineManager)Get(expedition, "_shrines"); Assert.That(shrines, Is.Not.Null);
                shrines.Activated += onActivated;
                Assert.That(shrines.Sample(site.Position, Vector3.forward, InputButtons.Interact, 1), Is.True);
                Assert.That(unresolved, Is.Empty);
                Assert.That(procedural.LinedPocketAnchors, Is.Not.Empty);
                Assert.That(((IDictionary)Get(floorState, "PocketStarts")).Contains(site.DestinationPocketRoomId), Is.True);
                Assert.That(shrines.Sample(site.Position, Vector3.forward, InputButtons.Interact, 2), Is.False);
                shrines.Activated -= onActivated;
                Call(expedition, "ReleaseFloor"); Assert.That(shrines == null, Is.True);
            }
            finally
            {
                expedition.ShrineWorldEffectUnresolved -= onUnresolved;
                if (shrines != null) { shrines.Activated -= onActivated; shrines.Teardown(); Object.DestroyImmediate(shrines.gameObject); }
                procedural.Teardown(); // This test owns the additional bake, including assertion-failure paths.
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void PurgatorySpawnsOnceAndAppliesOrPublishesMutation(bool pool)
        {
            var ground = Box(new Vector3(8, -.1f, 0), new Vector3(40, .2f, 20));
            var wall = Box(new Vector3(8, 2, 0), new Vector3(.4f, 4, 4));
            var sources = new List<NavMeshBuildSource>();
            foreach (var box in new[] { ground, wall }) sources.Add(new NavMeshBuildSource {
                shape = NavMeshBuildSourceShape.Box, transform = box.transform.localToWorldMatrix, size = Vector3.one, area = 0 });
            var data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources,
                new Bounds(new Vector3(8, 0, 0), new Vector3(44, 12, 24)), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null); owned.Add(data); navigation = NavMesh.AddNavMeshData(data); Physics.SyncTransforms();
            Assert.That(procedural.ValidateHunterSpawn(new Vector3(16, 0, 0), out _), Is.True);
            Assert.That(((ExpeditionSpawnDriver)Get(expedition, "_spawnDriver")).Validate(new Vector3(16, 0, 0), Vector3.zero,
                (ExpeditionSpawnDriverConfig)Get(expedition, "_spawnConfig")), Is.True);
            if (pool)
            {
                var mutation = new HunterMutationData(); Set(mutation, "_tellId", "fixture-tell"); Set(mutation, "_value", 1.3f);
                Set(profile, "_mutationPool", new[] { mutation });
            }
            var unresolved = new List<string>(); expedition.ShrineWorldEffectUnresolved += (_, reason) => unresolved.Add(reason);
            if (!pool) LogAssert.Expect(LogType.Warning, "Shrine 1: purgatory-mutation-unavailable:Hunter");
            var fact = Fact(ShrineKind.Purgatory, extra: 1, mutation: true);
            Call(expedition, "HandleShrineResolved", fact); Call(expedition, "HandleShrineResolved", fact);
            Assert.That(expedition.ActiveHunterCount, Is.EqualTo(2)); Assert.That(HunterRegistry.Items.Count, Is.EqualTo(1));
            foreach (var hunter in HunterRegistry.Items)
                Assert.That(((IDictionary)Get(hunter.ReadOnlyState, "Mutations")).Count, Is.EqualTo(pool ? 1 : 0));
            Assert.That(unresolved.Count, Is.EqualTo(pool ? 0 : 1));
        }
        [Test]
        public void PacificationReachesEveryRegisteredHunter()
        {
            for (int i = 0; i < 2; i++)
            {
                var id = factory.Spawn(new SpawnRequest("Hunter", new Vector3(16, 0, i), Quaternion.identity));
                Assert.That(HunterRegistry.TryGet(id, out var hunter), Is.True);
                Property(hunter.ReadOnlyState, "BeliefConfidence", 1f);
                Set(hunter.ReadOnlyState, "PendingNoiseDecision", true);
            }
            Call(expedition, "HandleShrineResolved", new ShrineResolvedFact(1,
                new ShrineActivatedFact(2, ShrineKind.Pacification, 1, Vector3.zero, 1), ShrineKind.Pacification, false, dropBeliefs: true));
            foreach (var hunter in HunterRegistry.Items)
            { Assert.That(hunter.ReadOnlyState.BeliefConfidence, Is.Zero); Assert.That(Get(hunter.ReadOnlyState, "PendingNoiseDecision"), Is.False); }
        }
        private static void ResetRegistries()
        {
            typeof(PlayerRegistry).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            typeof(HunterRegistry).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        }
        private static ShrineResolvedFact Fact(ShrineKind kind, int extra = 0, bool mutation = false) =>
            new ShrineResolvedFact(1, new ShrineActivatedFact(1, kind, 1, Vector3.zero, 1), kind, false, extraHunters: extra, mutation: mutation);
        private GameObject Box(Vector3 position, Vector3 size)
        { var box = new GameObject("fixture solid"); owned.Add(box); box.transform.position = position; box.transform.localScale = size; box.AddComponent<BoxCollider>(); return box; }
        private T Component<T>() where T : Component
        { var root = new GameObject(typeof(T).Name); owned.Add(root); root.SetActive(false); return root.AddComponent<T>(); }
        private T Config<T>() where T : ScriptableObject
        { var value = ScriptableObject.CreateInstance<T>(); owned.Add(value); return value; }
        private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
