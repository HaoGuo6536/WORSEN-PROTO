// ============================================================================
// HunterEchoIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Verifies duplicate factory identities, profile setup and native replay admission.
//   Remote temporary navigation surfaces prove the authored area mask affects
//   actual queries rather than merely being present on a config.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Check independent duplicate lives and idempotent asset creation.
//   - Check path, reaction and recording queries on a non-Walkable area.
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
                Assert.That(((HunterBehaviorState)a.ReadOnlyState).CatchActive, Is.True);
                Assert.That(((HunterBehaviorState)b.ReadOnlyState).CatchActive, Is.False);
                Assert.Throws<ArgumentOutOfRangeException>(() => first.Spawn(new HunterSpawnRequest(request, -1)));
            }
            finally
            {
                if (a != null) Object.DestroyImmediate(a.gameObject); if (b != null) Object.DestroyImmediate(b.gameObject);
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
                var driver = root.AddComponent<HunterDriver>(); driver.Initialize(config);
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
            { Object.DestroyImmediate(root); if (navigation.valid) navigation.Remove(); if (data != null) Object.DestroyImmediate(data); Object.DestroyImmediate(config); }
        }
    }
}
