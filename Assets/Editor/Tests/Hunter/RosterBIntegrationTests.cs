// ============================================================================
// RosterBIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Checks additive Manager/Driver seams and idempotent roster B profile setup.
//   Owned fixtures use distant geometry so no existing scene objects are changed.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Hunter.
// KEY RESPONSIBILITIES:
//   - Verify swept wall stops, Mimic normal-hit relay and preserved asset identities.
// DEPENDENCIES:
//   - Hunter runtime/setup, Core contracts, UnityEditor, physics and NUnit.
// USAGE NOTES:
//   Coordinator-only Edit Mode execution. These tests are compiled offline here,
//   not executed by a second editor. Test views are explicit injected observations.
//   Profile references use disposable persistent prefabs, never scene objects.
//   Runtime Manager lifecycle is paired explicitly; teardown runs even on failure.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Mimic;
using Worsen.Domain.Hunter.Archetypes.Ram;
using Worsen.Domain.Hunter.Archetypes.Skip;
using Worsen.Domain.Player;
using Worsen.Editor.Hunter;
using Object = UnityEngine.Object;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    public sealed class RosterBTestHunter : IReadOnlyHunterPursuitState
    {
        public EntityId Id { get; set; }
        public EntityId TargetId => new EntityId(1);
        public Vector3 Position { get; set; }
        public Vector3 Forward { get; set; } = Vector3.forward;
        public Vector3 Velocity => Vector3.zero;
        public bool PlayerVisible { get; set; }
        public Vector3 LastKnownPosition { get; set; }
        public long LastKnownTick => 0;
        public float BeliefConfidence => PlayerVisible ? 1f : 0f;
        public long Tick => 0;
        public bool IsActive { get; set; }
        public float LossSeconds => 2.5f;
        public float LossDistance => 14f;
        public bool PursuitSuppressed { get; set; }
    }
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000)]
    public sealed class RosterBIntegrationTests
    {
        [TestCase("Ram", 4, 8f, 1f, 1.05f, 2.5f)]
        [TestCase("Skip", 6, 4f, .6f, .2f, 0f)]
        [TestCase("Mimic", 5, 0f, 1.2f, 0f, 0f)]
        public void SetupReusesPlaceholderAndPreservesTuning(string name, int depth, float inertia, float commitment, float ratio, float loss)
        {
            string rootPath = "Assets/Editor/Tests/Hunter/RosterBSetup_" + Guid.NewGuid().ToString("N");
            var root = new GameObject("roster B placeholder"); root.SetActive(false); root.AddComponent<HunterManager>();
            try
            {
                AssetDatabase.CreateFolder("Assets/Editor/Tests/Hunter", System.IO.Path.GetFileName(rootPath));
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, rootPath + "/Placeholder.prefab");
                Assert.That(prefab, Is.Not.Null);
                var first = RosterBProfileSetup.BuildAssets(name, prefab, rootPath);
                string path = rootPath + "/" + name + "/" + name + "Profile.asset";
                string guid = AssetDatabase.AssetPathToGUID(path);
                string prefabGuid = AssetDatabase.AssetPathToGUID(rootPath + "/Placeholder.prefab");
                Assert.That(first.MinimumDepth, Is.EqualTo(depth)); Assert.That(first.Acceleration, Is.EqualTo(inertia));
                Assert.That(first.ActionCommitmentSeconds, Is.EqualTo(commitment)); Assert.That(first.ChaseSpeedMultiplier, Is.EqualTo(ratio));
                Assert.That(first.LossSeconds, Is.EqualTo(loss)); Assert.That(first.Prefab, Is.SameAs(prefab));
                EchoControllerTests.Tune(first, "_minimumDepth", 12);
                EchoControllerTests.Tune(first, "_acceleration", 3f);
                EchoControllerTests.Tune(first, "_turnRate", 33f);
                EchoControllerTests.Tune(first, "_actionCommitmentSeconds", 2f);
                EchoControllerTests.Tune(first, "_chaseSpeedMultiplier", .77f);
                EchoControllerTests.Tune(first, "_lossSeconds", 4f);
                EchoControllerTests.Tune(first, "_lossDistance", 21f);
                var rules = first.ArchetypeRules;
                EchoControllerTests.Tune(rules, name == "Ram" ? "_windupSeconds" : name == "Skip" ? "_cooldownSeconds" : "_biteSeconds", 2.3f);
                EditorUtility.SetDirty(first); EditorUtility.SetDirty(rules);
                AssetDatabase.SaveAssetIfDirty(first); AssetDatabase.SaveAssetIfDirty(rules);
                string tuning = EditorJsonUtility.ToJson(first), ruleTuning = EditorJsonUtility.ToJson(rules);
                string ruleGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(rules));
                var second = RosterBProfileSetup.BuildAssets(name, prefab, rootPath);
                Assert.That(second, Is.SameAs(first)); Assert.That(second.ArchetypeRules, Is.SameAs(rules));
                Assert.That(second.MinimumDepth, Is.EqualTo(12)); Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                Assert.That(second.Prefab, Is.SameAs(prefab));
                Assert.That(AssetDatabase.AssetPathToGUID(rootPath + "/Placeholder.prefab"), Is.EqualTo(prefabGuid));
                Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(rules)), Is.EqualTo(ruleGuid));
                Assert.That(EditorJsonUtility.ToJson(second), Is.EqualTo(tuning));
                Assert.That(EditorJsonUtility.ToJson(rules), Is.EqualTo(ruleTuning));

                // Model setup binds a different persistent body; profile setup must not undo it.
                var body = PrefabUtility.SaveAsPrefabAsset(root, rootPath + "/SelectedBody.prefab");
                Assert.That(body, Is.Not.Null);
                EchoControllerTests.Tune(first, "_prefab", body);
                EditorUtility.SetDirty(first); AssetDatabase.SaveAssetIfDirty(first);
                tuning = EditorJsonUtility.ToJson(first);
                Assert.That(RosterBProfileSetup.BuildAssets(name, prefab, rootPath).Prefab, Is.SameAs(body));
                Assert.That(EditorJsonUtility.ToJson(first), Is.EqualTo(tuning));
                EchoControllerTests.Tune(first, "_prefab", null);
                EchoControllerTests.Tune(first, "_archetypeRules", null);
                var repaired = RosterBProfileSetup.BuildAssets(name, prefab, rootPath);
                Assert.That(repaired.Prefab, Is.SameAs(prefab)); Assert.That(repaired.ArchetypeRules, Is.SameAs(rules));
                Assert.That(repaired.Acceleration, Is.EqualTo(3f));
                Assert.That(EditorJsonUtility.ToJson(rules), Is.EqualTo(ruleTuning));
            }
            finally { AssetDatabase.DeleteAsset(rootPath); Object.DestroyImmediate(root); }
        }
        [TestCase("Ram")] [TestCase("Skip")] [TestCase("Mimic")]
        public void SetupRejectsScenePlaceholderBeforeCreatingAssets(string name)
        {
            string path = "Assets/Editor/Tests/Hunter/RosterBInvalid_" + Guid.NewGuid().ToString("N");
            var root = new GameObject("not a prefab asset"); root.SetActive(false); root.AddComponent<HunterManager>();
            try
            {
                Assert.Throws<ArgumentException>(() => RosterBProfileSetup.BuildAssets(name, root, path));
                Assert.That(AssetDatabase.IsValidFolder(path), Is.False);
            }
            finally { AssetDatabase.DeleteAsset(path); Object.DestroyImmediate(root); }
        }
        [Test] public void NativeChargeDoesNotTurnStepOrContinueAfterWall()
        {
            Vector3 origin = new Vector3(4300, 100, 4300);
            var root = new GameObject("Ram motor fixture"); var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); var motor = ScriptableObject.CreateInstance<HunterMotorDriverConfig>();
            HunterDriver driver = null;
            try
            {
                root.transform.position = origin;
                floor.transform.position = origin - Vector3.up * .25f; floor.transform.localScale = new Vector3(20, .5f, 20);
                wall.transform.position = origin + Vector3.forward * 3 + Vector3.up; wall.transform.localScale = new Vector3(4, 2, .4f);
                driver = root.AddComponent<HunterDriver>(); driver.Initialize(motor); Physics.SyncTransforms();
                Assert.That(driver.MoveCharge(Vector3.forward, Vector3.forward, .1f, out _), Is.False);
                Assert.That(driver.Position.x, Is.EqualTo(origin.x));
                Assert.That(driver.MoveCharge(Vector3.forward * 5, Vector3.forward, .1f, out var blocker), Is.True);
                Assert.That(blocker, Is.SameAs(wall.GetComponent<Collider>())); Assert.That(driver.Position.z, Is.LessThan(origin.z + 3));
                Assert.That(driver.Position.y, Is.EqualTo(origin.y)); Assert.That(driver.Forward, Is.EqualTo(Vector3.forward));
                Assert.That(driver.Velocity, Is.EqualTo(Vector3.zero));
                Vector3 stopped = driver.Position; driver.MoveCharge(Vector3.zero, Vector3.forward, .1f, out _);
                Assert.That(driver.Position, Is.EqualTo(stopped));
            }
            finally { if (driver != null) driver.Teardown(); Object.DestroyImmediate(root); Object.DestroyImmediate(floor); Object.DestroyImmediate(wall); Object.DestroyImmediate(motor); }
        }
        [Test] public void NativeTeleportRequiresClearSupportedEndpointAndTransfersAtomically()
        {
            Vector3 origin = new Vector3(4500, 100, 4500);
            var root = new GameObject("Skip motor fixture"); var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); var motor = ScriptableObject.CreateInstance<HunterMotorDriverConfig>();
            NavMeshData data = null; NavMeshDataInstance nav = default;
            HunterDriver driver = null;
            try
            {
                root.transform.position = origin;
                floor.transform.position = origin - Vector3.up * .25f; floor.transform.localScale = new Vector3(20, .5f, 20);
                wall.transform.position = origin + Vector3.right * 100;
                var settings = NavMesh.GetSettingsByID(0); settings.overrideVoxelSize = true; settings.voxelSize = .1f;
                data = NavMeshBuilder.BuildNavMeshData(settings, new List<NavMeshBuildSource> {
                    new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, area = 0,
                        transform = Matrix4x4.TRS(floor.transform.position, Quaternion.identity, Vector3.one), size = floor.transform.localScale }
                }, new Bounds(origin, new Vector3(24, 10, 24)), Vector3.zero, Quaternion.identity);
                Assert.That(data, Is.Not.Null); nav = NavMesh.AddNavMeshData(data);
                driver = root.AddComponent<HunterDriver>(); driver.Initialize(motor); Physics.SyncTransforms();
                Assert.That(NavMesh.SamplePosition(origin + Vector3.forward * 4, out var target, 1f, 1), Is.True);
                Assert.That(driver.TryTeleport(target.position), Is.True);
                Assert.That(driver.Position, Is.EqualTo(target.position)); Assert.That(driver.Velocity, Is.EqualTo(Vector3.zero));
                wall.transform.position = origin + Vector3.right * 4 + Vector3.up;
                wall.transform.localScale = new Vector3(2, 2, 2); Physics.SyncTransforms();
                Vector3 before = driver.Position;
                Assert.That(driver.TryTeleport(origin + Vector3.right * 4), Is.False);
                Assert.That(driver.TryTeleport(origin + Vector3.right * 100), Is.False);
                Assert.That(driver.Position, Is.EqualTo(before));
            }
            finally
            {
                if (driver != null) driver.Teardown();
                Object.DestroyImmediate(root); Object.DestroyImmediate(floor); Object.DestroyImmediate(wall);
                if (nav.valid) nav.Remove(); if (data != null) Object.DestroyImmediate(data); Object.DestroyImmediate(motor);
            }
        }
        [Test] public void MimicManagerPublishesPoseAndExactlyOneNormalHitFromPhysicalTouch()
        {
            var root = new GameObject("Mimic fixture"); var playerRoot = new GameObject("touching player");
            var profile = ScriptableObject.CreateInstance<HunterProfile>(); var rules = ScriptableObject.CreateInstance<MimicConfig>();
            var motor = ScriptableObject.CreateInstance<HunterMotorDriverConfig>();
            HunterManager manager = null;
            var hits = new List<HunterHit>(); var facts = new List<MimicFact>();
            var order = new List<string>();
            void Hit(HunterHit hit) { hits.Add(hit); order.Add("hit"); }
            void Fact(MimicFact fact) { facts.Add(fact); if (fact.Kind == MimicFactKind.BiteStarted) order.Add("bite"); }
            try
            {
                Vector3 origin = new Vector3(4400, 100, 4400); root.transform.position = origin;
                playerRoot.transform.position = origin + Vector3.right * .5f + Vector3.up * .5f;
                var handle = playerRoot.AddComponent<TickingTestEntityHandle>(); handle.Value = new EntityId(1);
                var collider = playerRoot.AddComponent<BoxCollider>();
                EchoControllerTests.Tune(profile, "_archetypeRules", rules); EchoControllerTests.Tune(profile, "_motorOverride", motor);
                var player = new PlayerBehaviorState { Id = handle.Id, Health = 100, Position = playerRoot.transform.position };
                manager = root.AddComponent<HunterManager>();
                // Edit Mode does not invoke the runtime lifecycle. Pair before initialization:
                // OnDisable after Initialize would tear down the new Mimic's one-shot life.
                Lifecycle(manager, "OnDisable"); Lifecycle(manager, "OnEnable");
                manager.Initialize(profile, new EntityContext(new EntityId(-1), new System.Random(19)), player, new EchoControllerTests.World());
                var driver = root.GetComponent<HunterDriver>();
                var contacts = (Delegate)typeof(HunterDriver).GetField("OnLungeContact", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
                Assert.That(contacts, Is.Not.Null);
                Assert.That(contacts.GetInvocationList().Length, Is.EqualTo(1), "Exactly one real Manager contact subscription is required.");
                manager.OnLungeHit += Hit; manager.OnMimicFact += Fact;
                Physics.SyncTransforms();
                Vector3 center = origin + Vector3.up * rules.TouchRadius;
                Assert.That(Physics.OverlapSphere(center, rules.TouchRadius), Does.Contain(collider), "Native touch geometry must actually overlap.");
                Physics.SyncTransforms(); manager.Tick(.01f, 1); manager.Tick(.01f, 2);
                Assert.That(hits.Count, Is.EqualTo(1)); Assert.That(hits[0].Damage, Is.EqualTo(25));
                Assert.That(hits[0].Hunter, Is.EqualTo(manager.Id)); Assert.That(hits[0].Target, Is.EqualTo(handle.Id));
                Assert.That(hits[0].Source, Is.EqualTo(HitSource.Trap)); Assert.That(hits[0].Severity, Is.EqualTo(HitSeverity.Light));
                Assert.That(order, Is.EqualTo(new[] { "hit", "bite" }), "Session must receive hit intent before bite hold facts.");
                Assert.That(facts.Exists(f => f.Kind == MimicFactKind.Pose && !f.WhiteArrowEligible), Is.True);
                Assert.That(facts.FindAll(f => f.Kind == MimicFactKind.BiteStarted).Count, Is.EqualTo(1));
                Assert.That(facts.Find(f => f.Kind == MimicFactKind.BiteStarted).Seconds, Is.EqualTo(1.2f));
                Assert.That(player.Health, Is.EqualTo(100)); manager.Teardown();
                Assert.That(facts.FindAll(f => f.Kind == MimicFactKind.BiteEnded).Count, Is.EqualTo(1));
            }
            finally
            {
                if (manager != null)
                {
                    manager.OnLungeHit -= Hit; manager.OnMimicFact -= Fact;
                    Lifecycle(manager, "OnDisable"); manager.Teardown();
                }
                Object.DestroyImmediate(root); Object.DestroyImmediate(playerRoot); Object.DestroyImmediate(profile); Object.DestroyImmediate(rules); Object.DestroyImmediate(motor);
            }
        }
        private static void Lifecycle(HunterManager manager, string method) => typeof(HunterManager)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
        [TestCase("Ram")] [TestCase("Skip")] public void ManagerRegistersRemainingModules(string name)
        {
            var root = new GameObject("roster B registration"); var profile = ScriptableObject.CreateInstance<HunterProfile>();
            HunterArchetypeConfig rules = name == "Ram" ? (HunterArchetypeConfig)ScriptableObject.CreateInstance<RamConfig>() : ScriptableObject.CreateInstance<SkipConfig>();
            var motor = ScriptableObject.CreateInstance<HunterMotorDriverConfig>();
            HunterManager manager = null;
            try
            {
                EchoControllerTests.Tune(profile, "_archetypeRules", rules); EchoControllerTests.Tune(profile, "_motorOverride", motor);
                manager = root.AddComponent<HunterManager>();
                manager.Initialize(profile, new EntityContext(new EntityId(-1), new System.Random(1)),
                    new PlayerBehaviorState { Id = new EntityId(1), Health = 100 }, new EchoControllerTests.World());
                Assert.That(manager.BeginSkipFloor(1), Is.EqualTo(name == "Skip"));
            }
            finally { if (manager != null) manager.Teardown(); Object.DestroyImmediate(root); Object.DestroyImmediate(profile); Object.DestroyImmediate(rules); Object.DestroyImmediate(motor); }
        }
    }
}
