// ============================================================================
// WeaverIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Exercises real firing sweeps, partition links, ceiling colliders and setup.
//   Remote owned fixtures avoid changing any scene navigation or authored assets;
//   these tests are prepared here for coordinator-only Unity execution.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Verify engine probes cannot approve clipped webs and masks gate link crossing.
//   - Verify Manager publication, placeholder restoration and idempotent Resources wiring.
// DEPENDENCIES:
//   - Hunter runtime/editor tools, Core, Player state, UnityEditor/navigation and NUnit.
// USAGE NOTES:
//   Owns only temporary objects, NavMesh data/links and a unique test asset folder.
//   No global NavMesh removal or scene saves; all fixture resources are released.
// ============================================================================
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Weaver;
using Worsen.Domain.Player;
using Worsen.Editor.Hunter;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Hunter
{
    public sealed class WeaverTargetHandle : MonoBehaviour, IEntityHandle
    { public Worsen.Core.EntityId Id => new Worsen.Core.EntityId(1); }
    public sealed class WeaverIntegrationTests
    {
        private readonly Vector3 _origin = new Vector3(4200, 100, 4200);
        private readonly List<GameObject> _objects = new List<GameObject>();
        private HunterMotorDriverConfig _motor;
        private WeaverDriverConfig _config;
        private GameObject _root;
        private HunterDriver _driver;
        private WeaverWebDriver _web;
        private NavMeshData _data;
        private NavMeshDataInstance _navigation;
        private NavMeshLinkInstance _link;
        private GameObject Box(string name, Vector3 position, Vector3 size)
        {
            var root = new GameObject(name); _objects.Add(root); root.transform.position = position;
            root.AddComponent<BoxCollider>().size = size; return root;
        }
        [SetUp] public void SetUp()
        {
            _motor = ScriptableObject.CreateInstance<HunterMotorDriverConfig>(); _config = ScriptableObject.CreateInstance<WeaverDriverConfig>();
            EchoControllerTests.Tune(_motor, "_navigationAreaMask", 9);
            _root = new GameObject("Weaver fixture"); _objects.Add(_root); _root.transform.position = _origin;
            _driver = _root.AddComponent<HunterDriver>();
            CapsuleCollider capsule = _root.GetComponent<CapsuleCollider>(); capsule.center = Vector3.up * .9f; capsule.height = 1.8f; capsule.radius = .4f;
            _driver.Initialize(_motor); _driver.ConfigureWeaver(_config); _web = _root.GetComponent<WeaverWebDriver>();
        }
        [TearDown] public void TearDown()
        {
            _driver.Teardown();
            if (NavMesh.IsLinkValid(_link)) NavMesh.RemoveLink(_link);
            if (_navigation.valid) _navigation.Remove();
            if (_data != null) Object.DestroyImmediate(_data);
            foreach (GameObject root in _objects) if (root != null) Object.DestroyImmediate(root);
            _objects.Clear(); Object.DestroyImmediate(_motor); Object.DestroyImmediate(_config);
        }
        private void Bake(bool gap)
        {
            var sources = new List<NavMeshBuildSource>();
            if (gap)
                foreach (float x in new[] { -3f, 3f }) sources.Add(Source(_origin + Vector3.right * x, new Vector3(4, .5f, 10)));
            else sources.Add(Source(_origin, new Vector3(20, .5f, 20)));
            var settings = NavMesh.GetSettingsByID(0); settings.overrideVoxelSize = true; settings.voxelSize = .1f;
            _data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(_origin, new Vector3(30, 10, 30)), Vector3.zero, Quaternion.identity);
            Assert.That(_data, Is.Not.Null); _navigation = NavMesh.AddNavMeshData(_data);
        }
        private static NavMeshBuildSource Source(Vector3 position, Vector3 size) => new NavMeshBuildSource {
            shape = NavMeshBuildSourceShape.Box, area = 0, size = size,
            transform = Matrix4x4.TRS(position - Vector3.up * .25f, Quaternion.identity, Vector3.one) };
        [Test] public void ObstructedLineFindsSweptReachableRepositionAndRechecksAtLaunch()
        {
            Bake(false);
            Vector3 target = _origin + new Vector3(0, 1, 8);
            var wall = Box("Narrow firing obstruction", _origin + new Vector3(0, 1, 4), new Vector3(.6f, 2f, .6f));
            Physics.SyncTransforms();
            WeaverObservation blocked = _web.Probe(target, .06f, 15f, 1, null);
            Assert.That(blocked.Clear, Is.False);
            Assert.That(new List<WeaverShotSpot>(blocked.Spots).Exists(s => s.Clear && s.Reachable), Is.True);
            Assert.That(_web.Launch(_origin + Vector3.up, target, .06f, 11f, 15f, 1, null), Is.False);
            wall.SetActive(false); Physics.SyncTransforms();
            Assert.That(_web.Probe(target, .06f, 15f, 2, null).Clear, Is.True);
            wall.SetActive(true); Physics.SyncTransforms();
            Assert.That(_web.Launch(_origin + Vector3.up, target, .06f, 11f, 15f, 1, null), Is.False);
        }
        [Test] public void SmallRadiusSweepsDoorFrameAndStartingOverlapInsteadOfApprovingAClearRay()
        {
            Vector3 start = _origin + Vector3.up, end = start + Vector3.forward * 8;
            Box("Frame", start + new Vector3(.08f, 0, 4), new Vector3(.04f, 2, .3f)); Physics.SyncTransforms();
            Assert.That(_web.ClearSweep(start, end, .1f, null), Is.False);
            Assert.That(_web.ClearSweep(start, end, .02f, null), Is.True);
            Box("Muzzle overlap", start, Vector3.one * .1f); Physics.SyncTransforms();
            Assert.That(_web.ClearSweep(start, end, .02f, null), Is.False);
            Assert.That(_web.ClearSweep(start, end, .11f, null), Is.False);
        }
        [Test] public void ProjectileSweepsCannotTunnelAndWallInsertedAfterLaunchStopsAHit()
        {
            Vector3 start = _origin + Vector3.up, end = start + Vector3.forward * 8;
            Collider player = Box("Target", end, Vector3.one).GetComponent<Collider>(); Physics.SyncTransforms();
            Assert.That(_web.Launch(start, end, .06f, 11f, 15f, 1, c => c == player), Is.True);
            Box("Late wall", start + Vector3.forward * 4, new Vector3(2, 2, .1f)); Physics.SyncTransforms();
            Assert.That(_web.TickWebs(1f, c => c == player), Is.Empty);
            Assert.That(_web.TickWebs(1f, c => c == player), Is.Empty);
        }
        [Test] public void CeilingMovesBodyAndVisualTogetherThenRestoresThem()
        {
            var visual = new GameObject("Placeholder visual"); visual.transform.SetParent(_root.transform, false); visual.transform.localPosition = Vector3.up;
            _driver.ConfigureWeaver(_config); // Recapture the supplied prefab child.
            _driver.SetWeaverCeiling(_origin.y + 5f, true);
            Assert.That(_root.GetComponent<CapsuleCollider>().center.y, Is.EqualTo(.9f + 3.05f).Within(.001f));
            Assert.That(visual.transform.localPosition.y, Is.EqualTo(4.05f).Within(.001f));
            Assert.That(_driver.Position, Is.EqualTo(_origin), "Navigation remains on the floor.");
            _driver.SetWeaverCeiling(_origin.y + 5f, false);
            Assert.That(visual.transform.localPosition.y, Is.EqualTo(1f));
            _driver.SetWeaverCeiling(_origin.y + 5f, true); _driver.Teardown();
            Assert.That(_root.GetComponent<CapsuleCollider>().center.y, Is.EqualTo(.9f));
            Assert.That(visual.transform.localPosition.y, Is.EqualTo(1f));
        }
        [Test] public void OnlyAreaThreeLinkCanCrossPartitionAndOrdinaryMaskCannotUseIt()
        {
            Bake(true);
            foreach (float x in new[] { -3f, 3f }) Box("Link bank", _origin + Vector3.right * x - Vector3.up * .25f, new Vector3(4, .5f, 10));
            Assert.That(NavMesh.SamplePosition(_origin + Vector3.left * 2, out NavMeshHit a, 1f, 1), Is.True);
            Assert.That(NavMesh.SamplePosition(_origin + Vector3.right * 2, out NavMeshHit b, 1f, 1), Is.True);
            _root.transform.position = a.position;
            Box("Partition", _origin + Vector3.up, new Vector3(.6f, 2, 8)); Physics.SyncTransforms();
            Assert.That(_web.TryCrossPartition(new[] { a.position, b.position }, 0, out _), Is.False);
            _link = NavMesh.AddLink(new NavMeshLinkData { startPosition = a.position, endPosition = b.position, width = 0f, area = 3,
                agentTypeID = 0, bidirectional = true, costModifier = -1f });
            Assert.That(NavMesh.IsLinkValid(_link), Is.True);
            Assert.That(_web.TryCrossPartition(new[] { a.position, b.position }, 0, out Vector3 end), Is.True);
            Assert.That(end, Is.EqualTo(b.position));
            _driver.Move(b.position, 3, 16, 200, .1f, true, false, Vector3.zero, 0, 0);
            Assert.That(_driver.Position.x, Is.EqualTo(a.position.x), "A held warning must not cross a link.");
            for (int i = 0; i < 4; i++) _driver.Move(b.position, 3, 16, 200, .1f, false, false, Vector3.zero, 0, 0);
            Assert.That(_driver.Position.x, Is.EqualTo(b.position.x).Within(.1f));
            EchoControllerTests.Tune(_motor, "_navigationAreaMask", 1); _root.transform.position = a.position;
            Assert.That(_web.TryCrossPartition(new[] { a.position, b.position }, 0, out _), Is.False);
            var path = new NavMeshPath(); NavMesh.CalculatePath(a.position, b.position, 1, path);
            Assert.That(path.status, Is.Not.EqualTo(NavMeshPathStatus.PathComplete));
        }
        [Test] public void SetupPreservesFourAssetIdentitiesAndTuningAndOrdinaryDefaults()
        {
            string directory = "Assets/Editor/Tests/Hunter/WeaverSetup_" + Guid.NewGuid().ToString("N");
            _root.SetActive(false); _root.AddComponent<HunterManager>();
            try
            {
                HunterProfile first = WeaverProfileSetup.BuildAssets(directory, _root);
                string[] files = { "WeaverProfile", "WeaverConfig", "WeaverDriverConfig", "WeaverMotorDriverConfig" };
                var guids = Array.ConvertAll(files, file => AssetDatabase.AssetPathToGUID(directory + "/" + file + ".asset"));
                EchoControllerTests.Tune(first, "_chaseSpeedMultiplier", 1.19f);
                HunterProfile second = WeaverProfileSetup.BuildAssets(directory, _root);
                Assert.That(second, Is.SameAs(first)); Assert.That(second.ChaseSpeedMultiplier, Is.EqualTo(1.19f));
                Assert.That(second.ArchetypeRules, Is.TypeOf<WeaverConfig>()); Assert.That(((WeaverConfig)second.ArchetypeRules).DriverConfig, Is.Not.Null);
                Assert.That(second.MotorOverride.NavigationAreaMask, Is.EqualTo(9));
                Assert.That(Array.ConvertAll(files, file => AssetDatabase.AssetPathToGUID(directory + "/" + file + ".asset")), Is.EqualTo(guids));
                var ordinary = ScriptableObject.CreateInstance<HunterMotorDriverConfig>();
                try { Assert.That(ordinary.NavigationAreaMask, Is.EqualTo(1)); } finally { Object.DestroyImmediate(ordinary); }
            }
            finally { AssetDatabase.DeleteAsset(directory); }
        }
        [Test] public void ManagerPublishesWebHitAndCatchImmediatelyDropsTheCollider()
        {
            var profile = ScriptableObject.CreateInstance<HunterProfile>(); var rules = ScriptableObject.CreateInstance<WeaverConfig>();
            var hits = new List<WebHitFact>();
            HunterManager manager = null;
            try
            {
                Box("Floor", _origin - Vector3.up * .25f, new Vector3(30, .5f, 30));
                Vector3 target = _origin + Vector3.forward * 8;
                Box("Player", target + Vector3.up, Vector3.one).AddComponent<WeaverTargetHandle>();
                EchoControllerTests.Tune(rules, "_driverConfig", _config);
                EchoControllerTests.Tune(profile, "_archetypeRules", rules); EchoControllerTests.Tune(profile, "_motorOverride", _motor);
                EchoControllerTests.Tune(profile, "_sensorIntervalTicks", 1); EchoControllerTests.Tune(profile, "_lungeDistance", 1.2f);
                manager = _root.AddComponent<HunterManager>();
                var player = new PlayerBehaviorState { Id = new Worsen.Core.EntityId(1), Health = 100, SprintSpeed = 8, Position = target };
                var world = new EchoControllerTests.World { Graph = new LevelGraph(new[] { new LevelRoom(1, _origin + Vector3.up * 2.5f, new Vector3(30, 5, 30)) },
                    Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 1, _origin) };
                manager.Initialize(profile, new EntityContext(new Worsen.Core.EntityId(-9), new System.Random(7)), player, world);
                manager.OnWebHit += hits.Add; Physics.SyncTransforms();
                for (int i = 1; i <= 25; i++) manager.Tick(.1f, i);
                Assert.That(hits, Has.Count.EqualTo(1)); Assert.That(hits[0].SlowMultiplier, Is.EqualTo(.55f));
                _driver.SetWeaverCeiling(_origin.y + 5f, true); manager.BeginCatch(target);
                Assert.That(_root.GetComponent<CapsuleCollider>().center.y, Is.EqualTo(.9f));
            }
            finally
            { if (manager != null) { manager.OnWebHit -= hits.Add; manager.Teardown(); } Object.DestroyImmediate(profile); Object.DestroyImmediate(rules); }
        }
    }
}
