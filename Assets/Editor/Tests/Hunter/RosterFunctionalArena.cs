// ============================================================================
// RosterFunctionalArena.cs
// ============================================================================
// PURPOSE:
//   Builds a disposable native arena around the shipped Hunter profile and prefab.
//   Scripted player poses and camera samples exercise real sensing, navigation and
//   contact queries without Play Mode, focus, asset rebuilding or hidden state edits.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), test support (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Load real content and initialize its Manager, Driver and module explicitly.
//   - Own a remote two-room NavMesh, physical floor, camera and scripted player.
//   - Advance bounded fixed ticks and restore owned native resources on failure.
// DEPENDENCIES:
//   - Hunter, Player state, Core values, Expedition camera/world adapters and NUnit.
//   - UnityEditor asset reads and Unity native physics/navigation.
// USAGE NOTES:
//   Coordinator-only Edit Mode. No asset saves, global NavMesh clearing or physics
//   clock changes. Facts are Hunter outputs, not proof of Session damage or audio.
//   Construct first, Build inside try, Dispose in finally even if Build fails.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Ticking;
using Worsen.Domain.Player;
using Worsen.Session.Expedition;
using Object = UnityEngine.Object;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    internal sealed class RosterFunctionalArena : IDisposable
    {
        public const float Dt = 1f / 60f;
        public static readonly Vector3 Origin = new Vector3(7800f, 100f, 7800f);
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly Stopwatch _wall = Stopwatch.StartNew();
        private readonly Dictionary<Object, string> _content = new Dictionary<Object, string>();
        private NavMeshData _data;
        private NavMeshDataInstance _nav;
        private GameObject _playerObject;
        private Vector3 _previousPlayerPosition;
        private UnityEngine.Camera _camera;
        private ExpeditionViewDriver _view;
        private TickingDriver _clockDriver;
        private TickingManager _clock;
        public HunterProfile Profile { get; private set; }
        public HunterMotorDriverConfig Motor { get; private set; }
        public HunterManager Hunter { get; private set; }
        public HunterDriver Driver { get; private set; }
        public PlayerBehaviorState Player { get; } = new PlayerBehaviorState {
            Id = new EntityId(1), Health = 100, MaxHealth = 100, SprintSpeed = 8,
            Forward = Vector3.forward, Grounded = true };
        public RosterFunctionalWorld Level { get; } = new RosterFunctionalWorld();
        public RosterFunctionalMetrics Metrics { get; }
        public long Tick { get; private set; }
        public float Seconds => Tick * Dt;
        public Quaternion ViewRotation = Quaternion.identity;
        public GameObject Door { get; private set; }
        public RosterFunctionalArena(string name) { Metrics = new RosterFunctionalMetrics(name); }

        public void Build(string name, Vector3 hunterLocal, Vector3 playerLocal)
        {
            string path = "Assets/Resources/ScriptableObjects/Domain/Hunter/Archetypes/" + name + "/" + name + "Profile.asset";
            Profile = AssetDatabase.LoadAssetAtPath<HunterProfile>(path);
            Assert.That(Profile, Is.Not.Null, "Missing production profile: " + path + ". Run the approved profile/content setup before this fixture; no fallback is substituted.");
            Assert.That(Profile.ArchetypeKey, Is.EqualTo(name.ToLowerInvariant()));
            Assert.That(Profile.ArchetypeRules, Is.Not.Null, path + " has no module config.");
            Assert.That(Profile.Prefab, Is.Not.Null, path + " has no prefab.");
            Assert.That(EditorUtility.IsPersistent(Profile.Prefab), Is.True);
            Remember(Profile); Remember(Profile.ArchetypeRules);
            Assert.That(Profile.Prefab.activeSelf, Is.True, "The factory does not activate disabled prefabs.");
            var authoredManager = Profile.Prefab.GetComponent<HunterManager>();
            var authoredDriver = Profile.Prefab.GetComponent<HunterDriver>();
            Assert.That(authoredManager != null && authoredManager.enabled, Is.True, "Prefab Manager missing/disabled.");
            Assert.That(authoredDriver != null && authoredDriver.enabled, Is.True, "Prefab Driver missing/disabled.");
            // MotorOverride is optional: a null override means the prefab Driver's own motor config.
            Motor = Profile.MotorOverride != null ? Profile.MotorOverride : (HunterMotorDriverConfig)typeof(HunterDriver)
                .GetField("_config", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(authoredDriver);
            Assert.That(Motor, Is.Not.Null, path + " resolves no motor config (override or prefab Driver).");
            Remember(Motor);
            var authoredCapsule = Profile.Prefab.GetComponent<CapsuleCollider>();
            Assert.That(authoredCapsule != null && authoredCapsule.enabled && !authoredCapsule.isTrigger, Is.True, "Prefab body missing/disabled/trigger-only.");

            var floor = Box("roster floor", Origin - Vector3.up * .25f, new Vector3(64, .5f, 64));
            var settings = NavMesh.GetSettingsByID(0);
            settings.overrideVoxelSize = true; settings.voxelSize = .1f;
            _data = NavMeshBuilder.BuildNavMeshData(settings, new List<NavMeshBuildSource> {
                new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, area = 0,
                    transform = Matrix4x4.TRS(floor.transform.position, Quaternion.identity, Vector3.one), size = floor.transform.localScale }
            }, new Bounds(Origin, new Vector3(68, 12, 68)), Vector3.zero, Quaternion.identity);
            Assert.That(_data, Is.Not.Null, "Native arena bake returned no data.");
            _nav = NavMesh.AddNavMeshData(_data); Assert.That(_nav.valid, Is.True);
            Level.Graph = new LevelGraph(new[] {
                new LevelRoom(1, Origin + new Vector3(0, 3, -16), new Vector3(64, 8, 32)),
                new LevelRoom(2, Origin + new Vector3(0, 3, 16), new Vector3(64, 8, 32)) },
                new[] { new LevelEdge(7, 1, 2, true, TraversalAccess.All) }, Array.Empty<LevelAnchor>(), 2, Origin + Vector3.forward * 20);
            Level.Door = new InteractableState(101, InteractableKind.Door, 1, Origin + Vector3.up, InteractableStateValue.Open, 7);
            Level.Doors[7] = false;
            Door = Box("closed-door counterplay", Origin + Vector3.up * 1.5f, new Vector3(8, 3, .3f));
            Door.SetActive(false);
            _playerObject = Own(new GameObject("scripted roster player"));
            _playerObject.AddComponent<TickingTestEntityHandle>().Value = Player.Id;
            var capsule = _playerObject.AddComponent<CapsuleCollider>();
            capsule.radius = .3f; capsule.height = 1.8f; capsule.center = Vector3.up * .9f;
            var cameraObject = Own(new GameObject("scripted roster camera"));
            _camera = cameraObject.AddComponent<UnityEngine.Camera>();
            _camera.fieldOfView = 60f; _camera.aspect = 1.5f;
            _view = cameraObject.AddComponent<ExpeditionViewDriver>();
            typeof(ExpeditionViewDriver).GetField("_camera", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_view, _camera);
            SetPlayer(Point(playerLocal));
            _previousPlayerPosition = Player.Position;
            GameObject root = Own(Object.Instantiate(Profile.Prefab, Point(hunterLocal), Quaternion.identity));
            Hunter = root.GetComponent<HunterManager>(); Driver = root.GetComponent<HunterDriver>();
            Call(Hunter, "Awake");
            Call(Driver, "OnDisable"); Call(Driver, "OnEnable");
            Call(Hunter, "OnDisable"); Call(Hunter, "OnEnable");
            try { Initialize(); }
            finally { _clock = root.GetComponent<TickingManager>(); _clockDriver = root.GetComponent<TickingDriver>(); }
            if (_clock != null)
            {
                // The factory creates this facet during Initialize. Pair its callbacks
                // before a fresh life because OnDisable intentionally tears that life down.
                Call(_clock, "OnDisable"); Call(_clockDriver, "OnDisable");
                Call(_clockDriver, "OnEnable"); Call(_clock, "OnEnable"); Initialize();
            }
            var module = root.GetComponent<HunterArchetypeManager>();
            Assert.That(module, Is.Not.Null, "Factory failed to attach an archetype module.");
            Assert.That(module.GetType(), Is.EqualTo(HunterArchetypeFactory.BuiltIn.ModuleType(Profile.ArchetypeRules.GetType())));
            Assert.That(module.Rules, Is.Not.Null); Assert.That(module.enabled, Is.True);
            // Echo is deliberately hidden and inactive until its replay delay elapses (owner rule).
            Assert.That(Hunter.isActiveAndEnabled && Driver.isActiveAndEnabled && (Hunter.ReadOnlyState.IsActive || name == "Echo"), Is.True);
            Hunter.SetWorldView(new ExpeditionHunterController(new ExpeditionHunterBehaviorState(), 1, Level.Graph, Level));
            Hunter.SetClosedDoors(Level.Doors); Hunter.SetInteractables(Level); Hunter.SetFloorView(Level);
            Metrics.Attach(Hunter, Driver, Player.Id);
            Physics.SyncTransforms();
        }
        private void Initialize() => Hunter.Initialize(Profile, new EntityContext(new EntityId(-7001), new System.Random(23)), Player, Level);
        private void Remember(Object asset) { if (!_content.ContainsKey(asset)) _content.Add(asset, EditorJsonUtility.ToJson(asset)); }
        private GameObject Own(GameObject value) { _objects.Add(value); return value; }
        public GameObject Box(string name, Vector3 position, Vector3 size)
        {
            var value = Own(GameObject.CreatePrimitive(PrimitiveType.Cube)); value.name = name;
            value.transform.position = position; value.transform.localScale = size; return value;
        }
        public Vector3 Point(Vector3 local)
        {
            Assert.That(NavMesh.SamplePosition(Origin + local, out NavMeshHit hit, 1f, Motor.NavigationAreaMask), Is.True,
                "Scripted pose must lie on this arena's baked surface.");
            return hit.position;
        }
        public void SetPlayer(Vector3 position)
        {
            Player.Position = position;
            _playerObject.transform.position = position;
        }
        public void SetLit(bool lit)
        {
            // The production adapter reads this new lamp snapshot on its next tick.
            var lights = new List<InteractableState>();
            foreach (var room in Level.Graph.Rooms)
                lights.Add(new InteractableState(400000 + room.Id, InteractableKind.Light, room.Id,
                    room.Center, lit ? InteractableStateValue.Lit : InteractableStateValue.Inactive));
            Level.Lights = lights.AsReadOnly();
        }
        public void CloseDoor()
        {
            Level.Doors[7] = true;
            Level.Door = new InteractableState(101, InteractableKind.Door, 1, Origin + Vector3.up, InteractableStateValue.Inactive, 7);
            Door.SetActive(true); Physics.SyncTransforms();
        }
        public void Step()
        {
            Assert.That(_wall.Elapsed.TotalSeconds, Is.LessThan(55), "Per-case native work exceeded its 55s budget (5s reserved for cleanup).");
            Tick++; Player.Tick = Tick;
            Player.Velocity = (Player.Position - _previousPlayerPosition) / Dt;
            _previousPlayerPosition = Player.Position;
            _camera.transform.SetPositionAndRotation(Player.Position + Vector3.up * 1.5f, ViewRotation);
            Assert.That(_view.TrySample(Tick, out HunterPlayerView sample), Is.True);
            Hunter.SetPlayerView(sample); Physics.SyncTransforms();
            Hunter.Tick(Dt, Tick); Metrics.Observe(Seconds, Driver.Position);
            Assert.That(Vector3.Distance(Hunter.ReadOnlyState.Position, Driver.Position), Is.LessThan(.002f), "Manager must commit native Driver motion.");
        }
        public void For(float seconds, Action script = null)
        { int count = Mathf.CeilToInt(seconds / Dt); for (int i = 0; i < count; i++) { script?.Invoke(); Step(); } }
        public void Until(Func<bool> condition, float seconds, string failure)
        {
            int count = Mathf.CeilToInt(seconds / Dt);
            for (int i = 0; i < count && !condition(); i++) Step();
            Assert.That(condition(), Is.True, failure);
        }
        public static void Call(object target, string name)
        {
            Assert.That(target, Is.Not.Null, name + " owner missing.");
            var method = target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, target.GetType().Name + "." + name + " lifecycle changed.");
            method.Invoke(target, null);
        }
        public void Dispose()
        {
            try
            {
                Metrics.Write(Seconds, _wall.Elapsed.TotalSeconds);
                Metrics.Detach();
                try { if (Hunter != null) { Call(Hunter, "OnDisable"); Hunter.Teardown(); } }
                finally
                {
                    if (_clock != null) { Call(_clock, "OnDisable"); _clock.Teardown(); }
                    if (_clockDriver != null) { Call(_clockDriver, "OnDisable"); Call(_clockDriver, "OnDestroy"); }
                    if (Driver != null) { Call(Driver, "OnDisable"); Driver.Teardown(); }
                }
            }
            finally
            {
                if (_nav.valid) _nav.Remove();
                if (_data != null) Object.DestroyImmediate(_data);
                for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
                foreach (var pair in _content) Assert.That(EditorJsonUtility.ToJson(pair.Key), Is.EqualTo(pair.Value), "Production content was mutated: " + pair.Key.name);
                Assert.That(_wall.Elapsed.TotalSeconds, Is.LessThanOrEqualTo(60), "Case including cleanup exceeded 60 wall seconds.");
            }
        }
    }
}
