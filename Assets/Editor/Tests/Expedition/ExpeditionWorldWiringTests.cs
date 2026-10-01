// ============================================================================
// ExpeditionWorldWiringTests.cs
// ============================================================================
// PURPOSE:
//   Exercises floor-scoped world wiring with real Managers and a small physical door.
//   Pure state injection avoids generating navigation or loading a scene while the
//   assertions cover geometry, hearing delivery and symmetric failure cleanup.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Expedition.
// KEY RESPONSIBILITIES:
//   - Check committed Level state reaches Procedural geometry and current/late hunters.
//   - Require one Director noise route, with no direct duplicate hunter delivery.
//   - Preserve fallback diagnostics on failure and check both Director assembly call sites.
// DEPENDENCIES:
//   Core; Domain Level/Procedural/Hunter/Director/Player/Chase/Floor;
//   Session Expedition/HorrorEffects/Run; NUnit and Unity test-only object creation.
// USAGE NOTES:
//   Edit Mode only; no navigation build, scene load, persistent singleton initialization
//   or project asset writes. Reflection injects isolated state and invokes lifecycle seams.
//   Source-order assertions supplement, not replace, coordinator live assembly checks.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Chase;
using Worsen.Domain.Director;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Domain.Procedural;
using Worsen.Session.Expedition;
using Worsen.Session.HorrorEffects;
using Worsen.Session.Run;
using Object = UnityEngine.Object;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Expedition
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ExpeditionWorldWiringTests
    {
        private readonly List<Object> _owned = new List<Object>();
        private readonly List<HunterManager> _hunters = new List<HunterManager>();
        private ExpeditionSessionManager _expedition;
        private ExpeditionSessionController _controller;
        private RunSessionManager _run;
        private LevelManager _level;
        private ProceduralManager _procedural;
        private DirectorManager _director;
        private Collider _door;
        private readonly InteractableState _closed = new InteractableState(101, InteractableKind.Door,
            1, new Vector3(6f, 1f, 0f), InteractableStateValue.Inactive, 11);

        [SetUp]
        public void SetUp()
        {
            _run = Component<RunSessionManager>(); _expedition = Component<ExpeditionSessionManager>();
            _level = Component<LevelManager>(); _level.gameObject.SetActive(true);
            var graph = LevelGraphUtility.Build(new[] {
                new LevelRoom(1, new Vector3(0f, 2f, 0f), new Vector3(12f, 4f, 12f)),
                new LevelRoom(2, new Vector3(12f, 2f, 0f), new Vector3(12f, 4f, 12f)) },
                new[] { new LevelEdge(11, 1, 2, true, TraversalAccess.All) }, Array.Empty<LevelAnchor>(), 1, Vector3.zero);
            _level.InitializeGenerated(graph, new[] { _closed });
            _procedural = Component<ProceduralManager>();
            var driver = _procedural.GetComponent<ProceduralDriver>(); Set(_procedural, "_driver", driver);
            Property(Get(_procedural, "_state"), "IsReady", true);
            var physical = GameObject.CreatePrimitive(PrimitiveType.Cube); _owned.Add(physical);
            var worldObject = physical.AddComponent<ProceduralWorldObject>(); worldObject.Configure(_closed);
            _door = physical.GetComponent<Collider>();
            var geometry = (ProceduralDriverState)Get(driver, "_state");
            geometry.Ready = true; geometry.Interactables.Add(_closed.Id, worldObject);
            _director = Component<DirectorManager>();
            _director.Initialize(Config<DirectorConfig>(), new System.Random(17), new ChaseBehaviorState(), new FloorBehaviorState());
            _director.SetLevelView(_level.ReadOnlyState);
            var state = new ExpeditionSessionBehaviorState(); _controller = new ExpeditionSessionController(state);
            _controller.Bind(SceneKey.HorrorRun);
            _controller.Queue(new ProgressionGenerationRequest(1, 17, 1, false,
                new ProgressionEffects(1f, 1f, 1f, 1f, 100f, 100f, 0)));
            _controller.Begin(1);
            Set(_expedition, "_state", state); Set(_expedition, "_controller", _controller);
            Set(_expedition, "_run", _run); Set(_expedition, "_level", _level);
            Set(_expedition, "_procedural", _procedural); Set(_expedition, "_director", _director);
            Call(_expedition, "BindWorld");
        }

        [TearDown]
        public void TearDown()
        {
            if (_expedition != null) _expedition.ClearScene();
            foreach (var hunter in _hunters) if (hunter != null) hunter.Teardown();
            _hunters.Clear();
            for (int i = _owned.Count - 1; i >= 0; i--) if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
        }

        [Test]
        public void DoorChangesApplyGeometryAndAcousticsIncludingBeforeLateHuntersTick()
        {
            var first = Hunter(-701);
            Call(_expedition, "RouteClosedDoors");
            AssertDoors(first, true);
            Assert.That(Get(Get(_director, "_controller"), "_closedDoors"), Is.SameAs(_level.ClosedDoors));
            Assert.That(_door.enabled, Is.True);
            Assert.That(_level.OpenDoor(101), Is.True); Assert.That(_door.enabled, Is.False);
            AssertDoors(first, false);
            var late = Hunter(-702);
            Assert.That(Get(Get(late, "_controller"), "_closedDoors"), Is.Null);
            Publish(_run, "BeforeTick"); AssertDoors(late, false);
            Assert.That(_level.CloseDoor(101), Is.True); Assert.That(_door.enabled, Is.True);
            AssertDoors(first, true); AssertDoors(late, true);
            Assert.That(_level.Break(101), Is.True); Assert.That(_door.enabled, Is.False);
            AssertDoors(first, false); AssertDoors(late, false);
        }

        [TestCase(false)] [TestCase(true)]
        public void WorldSubscriptionsPairAcrossReleaseAndFailedAssembly(bool fail)
        {
            Call(_expedition, "BindWorld");
            Assert.That(Subscribers(_level, "InteractableChanged"), Is.EqualTo(1));
            Assert.That(Subscribers(_run, "BeforeTick"), Is.EqualTo(1));
            int releases = 0; _expedition.FloorReleased += () => releases++;
            if (fail)
            {
                _controller.RecordGenerationOutcome(true, "retained fallback manifest");
                LogAssert.Expect(LogType.Error, new Regex("usedFallback=True.*forced failure", RegexOptions.Singleline));
                Call(_expedition, "FailAssembly", 1, new InvalidOperationException("forced failure"));
                Assert.That(_expedition.UsedFallback, Is.True);
                Assert.That(_expedition.LayoutManifest, Is.EqualTo("retained fallback manifest"));
                Assert.That(_expedition.AssemblyPhase, Is.EqualTo(ExpeditionAssemblyPhase.Failed));
            }
            else Call(_expedition, "ReleaseFloor");
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(Subscribers(_level, "InteractableChanged"), Is.Zero);
            Assert.That(Subscribers(_run, "BeforeTick"), Is.Zero);
            _level.InitializeGenerated(LevelGraphUtility.Build(new[] {
                new LevelRoom(1, new Vector3(0f, 2f, 0f), new Vector3(12f, 4f, 12f)),
                new LevelRoom(2, new Vector3(12f, 2f, 0f), new Vector3(12f, 4f, 12f)) },
                new[] { new LevelEdge(11, 1, 2, true, TraversalAccess.All) }, Array.Empty<LevelAnchor>(), 1, Vector3.zero), new[] { _closed });
            Assert.That(_level.OpenDoor(101), Is.True);
            Assert.That(_door.enabled, Is.True, "Released geometry must no longer receive Level facts.");
        }

        [Test]
        public void SpawnCapacityFailureKeepsActionableReasonAfterCleanup()
        {
            const string reason = "hunter-spawn-capacity-shortfall: required=7, admitted=3, shortfall=4, nothingExtras=2";
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(reason)));
            Call(_expedition, "FailAssembly", 1, new InvalidOperationException(reason));
            Assert.That(_expedition.AssemblyPhase, Is.EqualTo(ExpeditionAssemblyPhase.Failed));
            Assert.That(_expedition.LastError, Does.Contain(reason));
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Session/Expedition/Manager/ExpeditionSessionManager.cs"));
            Assert.That(source, Does.Contain("hunter-spawn-capacity-shortfall: required="));
            Assert.That(source, Does.Contain("_progression.FailGeneration(generationId, _state.Failure)"));
        }

        [Test]
        public void EnvironmentalNoiseUsesDirectorOnceAndNoDirectHunterDuplicate()
        {
            var hunter = Hunter(-703);
            var effects = Component<HorrorEffectsManager>();
            var state = new HorrorEffectsBehaviorState();
            Set(effects, "controller", new HorrorEffectsController(state, Config<HorrorEffectsConfig>()));
            effects.ConfigureHazards(null, null, _director);
            var noise = new NoiseEvent(new EntityId(901), new Vector3(0f, 1f, 0f), 1f, 0);
            int emitted = 0, delivered = 0;
            effects.NoiseEmitted += fact => { Assert.That(fact, Is.EqualTo(noise)); emitted++; };
            _director.OnNoiseHintIssued += (id, fact) => { Assert.That(id, Is.EqualTo(hunter.Id)); delivered++; };
            state.Facts.Add(new HorrorEffectFact(HorrorEffectKind.Noise, noise: noise));
            Call(effects, "Publish"); Call(effects, "Publish");
            Assert.That(emitted, Is.EqualTo(1));
            Assert.That(((DirectorBehaviorState)Get(Get(_director, "_controller"), "_state")).Noises.Count, Is.EqualTo(1));
            var heard = (IList)Get(Get(hunter, "_state"), "HeardNoises");
            Assert.That(heard.Count, Is.Zero, "Bound noise must not also use the direct hunter path.");
            _director.Tick(.02f, 0); _director.Tick(.02f, 1);
            Assert.That(delivered, Is.EqualTo(1)); Assert.That(heard.Count, Is.EqualTo(1));
            effects.ClearHazards(); Assert.That(Get(effects, "director"), Is.Null);
        }

        [TestCase("Scripts/Session/Expedition/Manager/ExpeditionSessionManager.cs")]
        [TestCase("Scripts/Orchestrator/Scenes/FloorLoopSceneRoot.cs")]
        public void BothAssemblyPathsSetLevelImmediatelyAfterDirectorInitialization(string path)
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, path));
            Assert.That(Regex.IsMatch(source, @"_director\.Initialize\([^;]+;\s*_director\.SetLevelView\(_level\.ReadOnlyState\);"), Is.True);
        }

        private HunterManager Hunter(int id)
        {
            var hunter = Component<HunterManager>(); var profile = Config<HunterProfile>();
            var state = new HunterBehaviorState();
            var player = new PlayerBehaviorState { Id = new EntityId(901), Position = Vector3.up, Health = 100f };
            var controller = new HunterController(state, profile, new System.Random(17), player, _level.ReadOnlyState);
            controller.Reset(new EntityId(id), new Vector3(1f, 1f, 0f), Vector3.forward);
            Set(hunter, "_state", state); Set(hunter, "_controller", controller); Set(hunter, "_profile", profile);
            typeof(HunterRegistry).GetMethod("Register", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { hunter });
            _hunters.Add(hunter);
            return hunter;
        }
        private void AssertDoors(HunterManager hunter, bool closed)
        {
            var doors = (IReadOnlyDictionary<int, bool>)Get(Get(hunter, "_controller"), "_closedDoors");
            Assert.That(doors, Is.SameAs(_level.ClosedDoors)); Assert.That(doors[11], Is.EqualTo(closed));
        }
        private T Component<T>() where T : Component
        {
            var owner = new GameObject(typeof(T).Name + " world wiring test"); _owned.Add(owner);
            owner.SetActive(false); return owner.AddComponent<T>();
        }
        private T Config<T>() where T : ScriptableObject
        { var config = ScriptableObject.CreateInstance<T>(); _owned.Add(config); return config; }
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static object Get(object target, string name) => Field(target, name).GetValue(target);
        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private static void Publish(object target, string name) => (Get(target, name) as Action)?.Invoke();
        private static int Subscribers(object target, string name) => (Get(target, name) as Delegate)?.GetInvocationList().Length ?? 0;
    }
}
