// ============================================================================
// HunterEchoIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Verifies factory identities, native ghost replay and ordinary contact hits.
//   Remote temporary objects prove that replay needs no navigation, while the
//   retained navigation case protects the legacy motor's configured area mask.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Check independent duplicate lives and idempotent asset creation.
//   - Check native presence, exact height/facing, ghost doors and post-hit following.
//   - Preserve legacy path, reaction and recording queries on a non-Walkable area.
// DEPENDENCIES:
//   - Hunter runtime/editor tools, native Unity navigation, UnityEditor and NUnit.
// USAGE NOTES:
//   Coordinator-only Edit Mode execution. Owns only temporary objects, navigation
//   data and its unique test asset directory; never clears the scene's NavMesh.
// ============================================================================
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Echo;
using Worsen.Domain.Player;
using Worsen.Editor.Hunter;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000)]
    public sealed class HunterEchoIntegrationTests
    {
        [Test] public void ManagerReplayIsHiddenThenGhostsThroughDoorWithExactVaultPoseAndGraceHits()
        {
            var origin = new Vector3(7400, 100, 7400);
            var root = new GameObject("Echo mirror fixture");
            var target = new GameObject("Echo player fixture");
            var door = new GameObject("Closed door fixture");
            var profile = ScriptableObject.CreateInstance<HunterProfile>();
            var config = ScriptableObject.CreateInstance<EchoConfig>();
            var motor = ScriptableObject.CreateInstance<HunterMotorDriverConfig>();
            var playerProfile = ScriptableObject.CreateInstance<PlayerProfile>();
            HunterManager manager = null;
            int candidates = 0, accepted = 0;
            Action<HunterHit> hit = null;
            try
            {
                root.transform.position = origin + Vector3.left * 50;
                var renderer = root.AddComponent<MeshRenderer>();
                var hidden = new GameObject("Already hidden child"); hidden.transform.SetParent(root.transform);
                var hiddenRenderer = hidden.AddComponent<MeshRenderer>(); hiddenRenderer.forceRenderingOff = true;
                var disabledCollider = hidden.AddComponent<BoxCollider>(); disabledCollider.enabled = false;
                var handle = target.AddComponent<TickingTestEntityHandle>(); handle.Value = new Worsen.Core.EntityId(1);
                var capsule = target.AddComponent<CapsuleCollider>();
                capsule.height = 1.8f; capsule.radius = .35f; capsule.center = Vector3.up * .9f;
                target.AddComponent<BoxCollider>().center = Vector3.up * .9f; // Duplicate contact must not duplicate a candidate.
                door.transform.position = origin + new Vector3(2, 1, 0);
                door.AddComponent<BoxCollider>().size = new Vector3(.3f, 6f, 6f);
                var player = new PlayerBehaviorState { Id = handle.Id, Position = origin, Health = 100, MaxHealth = 100,
                    SprintSpeed = 8, RecoveryTickSeconds = 1f };
                EchoControllerTests.Tune(playerProfile, "_hitGraceSeconds", 2f);
                var health = new PlayerController(player, playerProfile, new System.Random(1));
                EchoControllerTests.Tune(profile, "_archetypeRules", config);
                EchoControllerTests.Tune(profile, "_motorOverride", motor); EchoControllerTests.Tune(profile, "_lungeDamage", 10);
                manager = root.AddComponent<HunterManager>();
                manager.Initialize(profile, new EntityContext(new Worsen.Core.EntityId(-1), new System.Random(3)), player, new EchoControllerTests.World());
                CallLifecycle(manager, "OnDisable"); CallLifecycle(manager, "OnEnable");
                hit = value =>
                {
                    candidates++;
                    Assert.That(value.Source, Is.EqualTo(HitSource.Other)); Assert.That(value.ContactNormal, Is.EqualTo(Vector3.zero));
                    if (health.ApplyHit(value.Damage, value.Severity).Changed) accepted++;
                };
                manager.OnLungeHit += hit;
                var driver = root.GetComponent<HunterDriver>(); var body = root.GetComponent<Rigidbody>();
                Assert.That(manager.ReadOnlyState.IsActive, Is.False);
                Assert.That(renderer.forceRenderingOff, Is.True);
                foreach (var collider in root.GetComponentsInChildren<Collider>()) Assert.That(collider.enabled, Is.False);
                manager.SetClosedDoors(new Dictionary<int, bool> { [7] = true });
                Vector3[] positions = { origin, origin + new Vector3(1, 2, 0), origin + Vector3.right * 3,
                    origin + new Vector3(5, 1, 0), origin + Vector3.right * 10, origin + Vector3.right * 10 };
                float[] headings = { 0, 90, 180, 270, 15, 25 };
                void Tick(int tick, Vector3 position, float heading)
                {
                    health.AdvanceRecovery(tick); player.Position = position; player.HeadingDegrees = heading;
                    target.transform.position = position; Physics.SyncTransforms(); manager.Tick(1f, tick);
                }
                for (int i = 1; i <= 5; i++)
                {
                    Tick(i, positions[i], headings[i]);
                    if (i < 4)
                    {
                        Assert.That(renderer.forceRenderingOff, Is.True); driver.ProbeBodyContact();
                        Assert.That(manager.ReadOnlyState.IsActive, Is.False);
                    }
                    else
                    {
                        Assert.That(renderer.forceRenderingOff, Is.False);
                        Assert.That(body.position, Is.EqualTo(positions[i - 4]));
                        Assert.That(Vector3.Distance(driver.Forward, Quaternion.Euler(0, headings[i - 4], 0) * Vector3.forward), Is.LessThan(.0001f));
                    }
                    Assert.That(candidates, Is.Zero);
                }
                Tick(6, positions[2], 70); Assert.That(body.position, Is.EqualTo(positions[2]));
                Assert.That(candidates, Is.EqualTo(1)); Assert.That(accepted, Is.EqualTo(1));
                manager.BeginCatch(player.Position);
                Tick(7, positions[3], 80); Assert.That(body.position, Is.EqualTo(positions[3]));
                Assert.That(accepted, Is.EqualTo(1), "Player grace, not Echo recovery, rejects a new overlapping tick.");
                Assert.That(((HunterBehaviorState)manager.ReadOnlyState).CatchActive, Is.False);
                Tick(8, positions[4], 90); Assert.That(body.position, Is.EqualTo(positions[4]));
                Assert.That(accepted, Is.EqualTo(2)); Assert.That(player.Health, Is.EqualTo(80));
                Tick(9, origin + Vector3.right * 30, 100);
                Assert.That(accepted, Is.EqualTo(2), "Moving away prevents further contacts.");
                Assert.That(manager.AttackSample.Phase, Is.Zero);
                Assert.That(hiddenRenderer.forceRenderingOff, Is.True);
                manager.Teardown();
                Assert.That(renderer.forceRenderingOff, Is.False); Assert.That(hiddenRenderer.forceRenderingOff, Is.True);
                Assert.That(root.GetComponent<CapsuleCollider>().enabled, Is.True); Assert.That(disabledCollider.enabled, Is.False);
            }
            finally
            {
                if (manager != null) { manager.OnLungeHit -= hit; CallLifecycle(manager, "OnDisable"); manager.Teardown(); }
                Object.DestroyImmediate(root); Object.DestroyImmediate(target); Object.DestroyImmediate(door);
                Object.DestroyImmediate(profile); Object.DestroyImmediate(config); Object.DestroyImmediate(motor); Object.DestroyImmediate(playerProfile);
            }
        }
        [Test] public void ReplaySweepsRecordedSegmentsButNeverSweepsFromTheOrdinarySpawn()
        {
            var root = new GameObject("Replay sweep fixture"); var target = new GameObject("Replay sweep target");
            var motor = ScriptableObject.CreateInstance<HunterMotorDriverConfig>(); HunterDriver driver = null;
            int contacts = 0; Action<Collider> contact = other => contacts++;
            try
            {
                Vector3 origin = new Vector3(7600, 100, 7600);
                root.transform.position = origin - Vector3.right * 10;
                target.transform.position = origin - Vector3.right * 5 + Vector3.up;
                var collider = target.AddComponent<BoxCollider>();
                driver = root.AddComponent<HunterDriver>(); driver.Initialize(motor); driver.SetTargetFilter(other => other == collider);
                driver.OnLungeContact += contact; driver.ConfigureKinematicReplay();
                var start = new HunterReplayPose(origin, 90);
                driver.MoveKinematicReplay(false, start, Array.Empty<HunterReplayPose>(), 1f); driver.ProbeBodyContact();
                driver.MoveKinematicReplay(true, start, new[] { start }, 1f);
                Assert.That(contacts, Is.Zero, "Appearance must not damage along the off-trail spawn-to-trail gap.");
                target.transform.position = origin + Vector3.right * 5 + Vector3.up; Physics.SyncTransforms();
                var end = new HunterReplayPose(origin + Vector3.right * 10, 180);
                driver.MoveKinematicReplay(true, end, new[] { end }, 1f);
                Assert.That(contacts, Is.GreaterThan(0), "Backlog consumption must not tunnel through the target.");
                Assert.That(driver.Position, Is.EqualTo(end.Position));
            }
            finally
            {
                if (driver != null) { driver.OnLungeContact -= contact; driver.Teardown(); }
                Object.DestroyImmediate(root); Object.DestroyImmediate(target); Object.DestroyImmediate(motor);
            }
        }
        private static void CallLifecycle(object target, string method) => target.GetType().GetMethod(method,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(target, null);
        [Test] public void DuplicateEchoesAcrossFactoriesHaveDistinctIdentitiesAndIndependentLives()
        {
            var prefab = new GameObject("Echo duplicate fixture"); prefab.SetActive(false); prefab.AddComponent<HunterManager>();
            var aRoot = new GameObject("Echo factory A"); var bRoot = new GameObject("Echo factory B");
            var profile = ScriptableObject.CreateInstance<HunterProfile>(); var motor = ScriptableObject.CreateInstance<HunterMotorDriverConfig>();
            var echo = ScriptableObject.CreateInstance<EchoConfig>();
            HunterManager a = null, b = null;
            try
            {
                EchoControllerTests.Tune(profile, "_archetypeKey", "echo"); EchoControllerTests.Tune(profile, "_prefab", prefab);
                EchoControllerTests.Tune(profile, "_archetypeRules", echo); EchoControllerTests.Tune(profile, "_motorOverride", motor);
                EchoControllerTests.Tune(echo, "_delaySeconds", .1f);
                var player = new PlayerBehaviorState { Id = new Worsen.Core.EntityId(1), Health = 100, SprintSpeed = 8 };
                var world = new EchoControllerTests.World(); var random = new System.Random(7);
                var first = aRoot.AddComponent<HunterFactory>(); var second = bRoot.AddComponent<HunterFactory>();
                first.Configure(profile, random, player, world); second.Configure(profile, random, player, world);
                var request = new SpawnRequest("echo", Vector3.zero, Quaternion.identity);
                var aId = first.Spawn(new HunterSpawnRequest(request, 0)); var bId = second.Spawn(new HunterSpawnRequest(request, 1));
                Assert.That(aId, Is.Not.EqualTo(bId));
                Assert.That(HunterRegistry.TryGet(aId, out a), Is.True); Assert.That(HunterRegistry.TryGet(bId, out b), Is.True);
                Assert.That(a.DuplicateIndex, Is.Zero); Assert.That(b.DuplicateIndex, Is.EqualTo(1));
                Assert.That(a.ReadOnlyState, Is.Not.SameAs(b.ReadOnlyState));
                var published = new List<HunterArchetypeFact>();
                a.OnArchetypeFact += published.Add;
                try
                {
                    a.SetActiveEffects(new ActiveEffects(new[] { new ActiveEffect(EchoController.TrailReader, EffectKind.Upgrade, 1) }));
                    player.Position = Vector3.right; a.Tick(.1f, 1); b.Tick(.1f, 1);
                    player.LookBack = true; player.Position = Vector3.right * 2; a.Tick(.1f, 2);
                    Assert.That(published.Exists(f => f.Kind == HunterArchetypeFactKind.TrailRevealed && f.Hunter == aId), Is.True);
                    Assert.That(b.ReadOnlyState.Tick, Is.EqualTo(1));
                }
                finally { a.OnArchetypeFact -= published.Add; }
                Assert.That(a.ApplyMutation(new HunterMutation(HunterTunable.ChaseSpeedMultiplier, 1.4f, "quickened")), Is.True);
                a.BeginCatch(Vector3.zero);
                Assert.That(((HunterBehaviorState)a.ReadOnlyState).CatchActive, Is.False, "Replay cannot be held after a hit.");
                Assert.That(((HunterBehaviorState)b.ReadOnlyState).CatchActive, Is.False);
                Assert.Throws<ArgumentOutOfRangeException>(() => first.Spawn(new HunterSpawnRequest(request, -1)));
            }
            finally
            {
                if (a != null) { a.Teardown(); Object.DestroyImmediate(a.gameObject); }
                if (b != null) { b.Teardown(); Object.DestroyImmediate(b.gameObject); }
                Object.DestroyImmediate(aRoot); Object.DestroyImmediate(bRoot); Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(profile); Object.DestroyImmediate(motor); Object.DestroyImmediate(echo);
            }
        }
        [Test] public void EchoSetupPreservesIdentityAndTuningWhileRepairingOwnedReferences()
        {
            string directory = "Assets/Editor/Tests/Hunter/EchoSetup_" + Guid.NewGuid().ToString("N");
            var prefab = new GameObject("Echo setup fixture"); prefab.SetActive(false); prefab.AddComponent<HunterManager>();
            try
            {
                HunterProfile first = EchoProfileSetup.BuildAssets(directory, prefab);
                string guid = AssetDatabase.AssetPathToGUID(directory + "/EchoProfile.asset");
                var so = new SerializedObject(first); so.FindProperty("_chaseSpeedMultiplier").floatValue = 1.15f;
                so.ApplyModifiedPropertiesWithoutUndo();
                HunterProfile second = EchoProfileSetup.BuildAssets(directory, prefab);
                Assert.That(second, Is.SameAs(first)); Assert.That(second.ChaseSpeedMultiplier, Is.EqualTo(1.15f));
                Assert.That(AssetDatabase.AssetPathToGUID(directory + "/EchoProfile.asset"), Is.EqualTo(guid));
                Assert.That(second.ArchetypeKey, Is.EqualTo("echo")); Assert.That(second.ArchetypeRules, Is.TypeOf<EchoConfig>());
                Assert.That(second.NeverLoses, Is.True); Assert.That(second.LossSeconds, Is.EqualTo(float.PositiveInfinity));
                Assert.That(second.LossDistance, Is.EqualTo(float.PositiveInfinity));
                Assert.That(second.MotorOverride.NavigationAreaMask, Is.EqualTo(1));
            }
            finally { AssetDatabase.DeleteAsset(directory); Object.DestroyImmediate(prefab); }
        }
        [Test] public void ConfiguredAreaMaskControlsPathReactionAndExactRecordingQueries()
        {
            var origin = new Vector3(3200, 100, 3200);
            var config = ScriptableObject.CreateInstance<HunterMotorDriverConfig>(); var root = new GameObject("Echo area fixture");
            HunterDriver driver = null;
            NavMeshData data = null; NavMeshDataInstance navigation = default;
            try
            {
                var settings = NavMesh.GetSettingsByID(0); settings.overrideVoxelSize = true; settings.voxelSize = .1f;
                data = NavMeshBuilder.BuildNavMeshData(settings, new List<NavMeshBuildSource> {
                    new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, area = 3,
                        transform = Matrix4x4.TRS(origin - Vector3.up * .25f, Quaternion.identity, Vector3.one), size = new Vector3(12, .5f, 12) } },
                    new Bounds(origin, new Vector3(20, 10, 20)), Vector3.zero, Quaternion.identity);
                Assert.That(data, Is.Not.Null); navigation = NavMesh.AddNavMeshData(data);
                Assert.That(NavMesh.SamplePosition(origin, out NavMeshHit start, 1f, 1 << 3), Is.True);
                root.transform.position = start.position;
                driver = root.AddComponent<HunterDriver>(); driver.Initialize(config);
                Vector3 target = start.position + Vector3.forward * 2f;
                Assert.That(config.NavigationAreaMask, Is.EqualTo(1));
                driver.Move(target, 1f, 20f, 240f, .1f, false, false, Vector3.zero, 0f, 0f);
                Assert.That(driver.PathAvailable, Is.False); Assert.That(driver.ValidateReactionTarget(target), Is.False);
                Assert.That(driver.MoveRecording(new[] { target }, .1f), Is.Zero);
                var so = new SerializedObject(config); so.FindProperty("_navigationAreaMask").intValue = 1 | (1 << 3); so.ApplyModifiedPropertiesWithoutUndo();
                root.transform.position = start.position; driver.Initialize(config);
                driver.Move(target, 1f, 20f, 240f, .01f, false, false, Vector3.zero, 0f, 0f);
                Assert.That(driver.PathAvailable, Is.True); Assert.That(driver.ValidateReactionTarget(target), Is.True);
                Assert.That(driver.MoveRecording(new[] { target }, .1f), Is.EqualTo(1));
                Assert.That(driver.Position, Is.EqualTo(target));
            }
            finally
            { if (driver != null) driver.Teardown(); Object.DestroyImmediate(root); if (navigation.valid) navigation.Remove(); if (data != null) Object.DestroyImmediate(data); Object.DestroyImmediate(config); }
        }
    }
}
