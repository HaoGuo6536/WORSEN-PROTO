// ============================================================================
// TickingIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Checks native key admission, module routing and idempotent setup after import.
//   These fixtures own temporary navigation and objects, never the loaded scene.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Consume promoted Core sound/guidance/noise facts with their hunter attribution.
//   - Reject gap-separated candidates and verify key touch identity and fact routing.
// DEPENDENCIES:
//   - Hunter runtime/setup, Core/Player, Unity navigation, UnityEditor and NUnit.
// USAGE NOTES:
//   Coordinator-only Edit Mode execution. Trigger callbacks are dispatched explicitly;
//   OnEnable/OnDisable are paired explicitly because these are not ExecuteAlways components.
//   an actual moving-player physics pass remains a live integration check.
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
using Worsen.Domain.Hunter.Archetypes.Ticking;
using Worsen.Domain.Player;
using Worsen.Editor.Hunter;
using Object = UnityEngine.Object;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000)]
    public sealed class TickingIntegrationTests
    {
        [Test] public void SetupReusesAssetsPreservesTuningAndRepairsReferences()
        {
            string directory = "Assets/Editor/Tests/Hunter/TickingSetup_" + Guid.NewGuid().ToString("N");
            var hunter = new GameObject("clock fixture"); var key = new GameObject("key fixture");
            hunter.SetActive(false); hunter.AddComponent<HunterManager>();
            try
            {
                var first = TickingProfileSetup.BuildAssets(directory, hunter, key);
                string guid = AssetDatabase.AssetPathToGUID(directory + "/TickingProfile.asset");
                EchoControllerTests.Tune(first, "_chaseSpeedMultiplier", 1.3f);
                var rules = (TickingConfig)first.ArchetypeRules; EchoControllerTests.Tune(rules, "_springSeconds", 60f);
                var second = TickingProfileSetup.BuildAssets(directory, hunter, key);
                Assert.That(second, Is.SameAs(first)); Assert.That(second.ChaseSpeedMultiplier, Is.EqualTo(1.3f));
                Assert.That(rules.SpringSeconds, Is.EqualTo(60f)); Assert.That(rules.DriverConfig, Is.Not.Null);
                Assert.That(AssetDatabase.AssetPathToGUID(directory + "/TickingProfile.asset"), Is.EqualTo(guid));
                Assert.That(second.ArchetypeKey, Is.EqualTo("ticking")); Assert.That(second.NeverLoses, Is.False);
                Assert.That(second.Habits.Count, Is.EqualTo(1)); Assert.That(second.Habits[0].Kind, Is.EqualTo(HunterHabitKind.ThresholdPause));
                Assert.That(second.MotorOverride.NavigationAreaMask, Is.EqualTo(1));
            }
            finally { AssetDatabase.DeleteAsset(directory); Object.DestroyImmediate(hunter); Object.DestroyImmediate(key); }
        }
        [Test] public void NativePlacementRejectsDisconnectedGapAndPhysicalWall()
        {
            var origin = new Vector3(4200, 100, 4200);
            var root = new GameObject("Ticking navigation fixture"); var key = new GameObject("key fixture");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var config = ScriptableObject.CreateInstance<TickingDriverConfig>(); NavMeshData data = null; NavMeshDataInstance nav = default;
            try
            {
                EchoControllerTests.Tune(config, "_keyPrefab", key);
                var settings = NavMesh.GetSettingsByID(0); settings.overrideVoxelSize = true; settings.voxelSize = .1f;
                var sources = new List<NavMeshBuildSource>();
                foreach (float x in new[] { 0f, 16f }) sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    area = 0, transform = Matrix4x4.TRS(origin + Vector3.right * x - Vector3.up * .25f, Quaternion.identity, Vector3.one),
                    size = new Vector3(12, .5f, 12) });
                data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(origin + Vector3.right * 8, new Vector3(40, 10, 20)), Vector3.zero, Quaternion.identity);
                Assert.That(data, Is.Not.Null); nav = NavMesh.AddNavMeshData(data);
                var driver = root.AddComponent<TickingDriver>(); driver.Initialize(config);
                wall.transform.position = origin + Vector3.right * 100; Physics.SyncTransforms();
                Assert.That(driver.TrySampleKey(origin, origin + Vector3.forward * 4, null, out _), Is.True);
                Assert.That(driver.TrySampleFollow(origin, origin + Vector3.forward * 4, out _), Is.True);
                Assert.That(driver.TrySampleKey(origin, origin + Vector3.right * 16, null, out _), Is.False);
                Assert.That(driver.TrySampleFollow(origin, origin + Vector3.right * 16, out _), Is.False);
                wall.transform.position = origin + Vector3.forward * 2 + Vector3.up;
                wall.transform.localScale = new Vector3(4, 2, .4f); Physics.SyncTransforms();
                Assert.That(driver.TrySampleKey(origin, origin + Vector3.forward * 4, null, out _), Is.False);
            }
            finally
            { Object.DestroyImmediate(root); Object.DestroyImmediate(key); Object.DestroyImmediate(wall); if (nav.valid) nav.Remove(); if (data != null) Object.DestroyImmediate(data); Object.DestroyImmediate(config); }
        }
        [Test] public void ManagerRegistersModuleRelaysSoundGuidanceNoiseAndClearsOwnedKey()
        {
            var root = new GameObject("Ticking manager fixture"); var playerRoot = new GameObject("collector");
            var keyPrefab = new GameObject("key fixture"); var stranger = new GameObject("stranger");
            IHunterTickingModule ticking = null; TickingDriver driver = null;
            var config = ScriptableObject.CreateInstance<TickingConfig>(); var driverConfig = ScriptableObject.CreateInstance<TickingDriverConfig>();
            var profile = ScriptableObject.CreateInstance<HunterProfile>(); var motor = ScriptableObject.CreateInstance<HunterMotorDriverConfig>();
            try
            {
                EchoControllerTests.Tune(config, "_driverConfig", driverConfig); EchoControllerTests.Tune(driverConfig, "_keyPrefab", keyPrefab);
                EchoControllerTests.Tune(profile, "_archetypeRules", config); EchoControllerTests.Tune(profile, "_motorOverride", motor);
                EchoControllerTests.Tune(profile, "_archetypeKey", "ticking");
                var player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100, SprintSpeed = 8 };
                var manager = root.AddComponent<HunterManager>(); manager.Initialize(profile, new EntityContext(new EntityId(-10), new System.Random(3)), player, new EchoControllerTests.World());
                Assert.That(manager.Ticking, Is.Not.Null);
                ticking = manager.Ticking; driver = root.GetComponent<TickingDriver>();
                // Establish one subscription per owner even if Unity delivered no Edit Mode callbacks.
                Call(ticking, "OnDisable"); Call(driver, "OnDisable");
                Call(driver, "OnEnable"); Call(ticking, "OnEnable");
                manager.Initialize(profile, new EntityContext(new EntityId(-10), new System.Random(3)), player, new EchoControllerTests.World());
                var sounds = new List<string>(); var noises = new List<NoiseEvent>(); var arrows = new List<GuidanceTarget>();
                manager.Ticking.OnSound += fact => { Assert.That(fact.Hunter, Is.EqualTo(manager.Id)); sounds.Add(fact.SoundId); };
                manager.Ticking.OnNoise += fact => { Assert.That(fact.Hunter, Is.EqualTo(manager.Id)); noises.Add(fact.Noise); };
                manager.Ticking.OnGuidance += fact => { if (fact.Active) arrows.Add(fact.Target); };
                manager.SetActiveEffects(new ActiveEffects(new[] { new ActiveEffect(TickingController.LoudKeys, EffectKind.Curse, 1) }));
                manager.Tick(30f, 1); Assert.That(sounds, Does.Contain("ticking.tick"));
                var field = typeof(TickingManager).GetField("_controller", BindingFlags.NonPublic | BindingFlags.Instance);
                var clock = (TickingController)field.GetValue(manager.Ticking);
                manager.Tick(1f, 2); // Failed native placement retries, never invents a key.
                clock.Tick(new HunterArchetypeContext(manager.ReadOnlyState, player, new EchoControllerTests.World(), null, null, null,
                    new ActiveEffects(new[] { new ActiveEffect(TickingController.LoudKeys, EffectKind.Curse, 1) }), 1f, 3, true, 1));
                Assert.That(clock.PlaceKey(Vector3.right * 8, true), Is.True); manager.Ticking.PublishTick();
                Assert.That(arrows.Count, Is.GreaterThan(0)); Assert.That(arrows[0].Kind, Is.EqualTo(GuidanceKind.ThreatArrow));
                var driverState = (TickingDriverState)typeof(TickingDriver).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(driver);
                var spawned = driverState.Key; Assert.That(spawned, Is.Not.Null);
                var contact = spawned.GetComponent<TickingKeyContact>();
                Call(contact, "OnTriggerEnter", stranger.AddComponent<BoxCollider>()); Assert.That(clock.HasKey, Is.True);
                var handle = playerRoot.AddComponent<TickingTestEntityHandle>(); handle.Value = player.Id;
                Call(contact, "OnTriggerEnter", playerRoot.AddComponent<BoxCollider>());
                Assert.That(clock.HasKey, Is.False); Assert.That(driverState.Key, Is.Null);
                Assert.That(sounds, Does.Contain("ticking.winding")); Assert.That(noises.Count, Is.EqualTo(1));
                manager.Teardown(); Assert.That(driverState.Key, Is.Null);
                Call(ticking, "OnDisable"); Call(driver, "OnDisable");
                var relay = (Delegate)typeof(TickingDriver).GetField("OnKeyContact", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(driver);
                Assert.That(relay, Is.Null);
            }
            finally
            {
                if (ticking != null) Call(ticking, "OnDisable");
                if (driver != null) Call(driver, "OnDisable");
                Object.DestroyImmediate(root); Object.DestroyImmediate(playerRoot); Object.DestroyImmediate(stranger); Object.DestroyImmediate(keyPrefab);
                Object.DestroyImmediate(config); Object.DestroyImmediate(driverConfig); Object.DestroyImmediate(profile); Object.DestroyImmediate(motor);
            }
        }
        private static void Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    }
    public sealed class TickingTestEntityHandle : MonoBehaviour, IEntityHandle
    {
        public EntityId Value;
        public EntityId Id => Value;
    }
}
