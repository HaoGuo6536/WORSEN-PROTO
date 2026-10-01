// ============================================================================
// FloorLumenRegenerationTests.cs
// ============================================================================
// PURPOSE:
//   Builds and replaces the real Floor, Environment and Horror lighting owners.
//   Captured identities must disappear even when lights were pooled or detached.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Floor integration.
// KEY RESPONSIBILITIES:
//   - Exercise teardown/rebuild with cake, exit, warning, lamp, ray and Horror effects.
//   - Assert old components leave both the scene and Lumen's global registries.
//   - Preserve unrelated players and shared profiles across repeated replacement.
// DEPENDENCIES:
//   - Core, Floor/Environment/Horror Drivers, Lumen, UnityEditor and NUnit.
// USAGE NOTES:
//   Native Edit Mode only; coordinator runs under the Unity lease. No scene saves.
//   Preview lighting isolation protects open scenes. Every owner is torn down in
//   finally, including components whose Edit Mode OnDestroy is not invoked.
// ============================================================================
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using DistantLands.Lumen;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Presentation.Environment;
using Worsen.Presentation.Horror;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Floor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000)]
    public sealed class FloorLumenRegenerationTests
    {
        [TestCase(false)] [TestCase(true)]
        public void RebuildReleasesEveryOldFloorPlayerIncludingDetachedAndPooledEffects(bool physicalDoor)
        {
            Assert.That(Application.isPlaying, Is.False);
            var previousManagers = Resources.FindObjectsOfTypeAll<LumenManager>();
            var scene = EditorSceneManager.NewPreviewScene();
            FloorDriver floor = null; EnvironmentDriver environment = null; HorrorDriver horror = null;
            FloorDriverConfig floorConfig = null; EnvironmentDriverConfig environmentConfig = null;
            HorrorDriverConfig horrorConfig = null; LumenEffectProfile profile = null;
            AudioClip clip = null;
            Unsupported.SetOverrideLightingSettings(scene);
            try
            {
                var imported = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.distantlands.lumen/Content/Prefabs/Lantern Effect.prefab");
                Assert.That(imported, Is.Not.Null);
                profile = Object.Instantiate(imported.GetComponentInChildren<LumenEffectPlayer>(true).profile);
                profile.autoAssignSun = false;
                profile.layers.RemoveAll(layer => !(layer is LumenLightLayer));
                Assert.That(profile.layers, Is.Not.Empty);
                foreach (LumenLightLayer layer in profile.layers)
                { layer.range = 2f; layer.intensity = .2f; layer.fluctuation = false; }
                string profileBefore = JsonUtility.ToJson(profile);
                var prefab = Root(scene, "Private Lumen template"); prefab.SetActive(false);
                prefab.AddComponent<LumenEffectPlayer>().profile = profile;
                var unrelated = Root(scene, "Unrelated Lumen owner").AddComponent<FloorLumenGlow>();
                unrelated.Configure(prefab, 2f, Color.white, 1f, true);
                var unrelatedPlayer = unrelated.GetComponentInChildren<LumenEffectPlayer>(true);

                floorConfig = Worsen.Tests.Core.ShaderReferenceTestSetup.Create<FloorDriverConfig>();
                Set(floorConfig, "_lumenRoomWarningPrefab", prefab); Set(floorConfig, "_lumenExitGlowPrefab", prefab);
                Set(floorConfig, "_usePhysicalExitDoor", physicalDoor);
                floor = Root(scene, "Floor owner").AddComponent<FloorDriver>(); Set(floor, "_config", floorConfig);
                environmentConfig = Worsen.Tests.Core.ShaderReferenceTestSetup.Create<EnvironmentDriverConfig>();
                Set(environmentConfig, "_lumenLanternPrefab", prefab); Set(environmentConfig, "_lumenMoonPrefab", prefab);
                environment = Root(scene, "Environment owner").AddComponent<EnvironmentDriver>(); environment.Initialize(environmentConfig);
                horrorConfig = Worsen.Tests.Core.ShaderReferenceTestSetup.Create<HorrorDriverConfig>();
                Set(horrorConfig, "_lumenFlashlightPrefab", prefab); Set(horrorConfig, "_lumenNearFillPrefab", prefab);
                clip = AudioClip.Create("Silent fixture growl", 64, 1, 8000, false); Set(horrorConfig, "_attackGrowl", clip);
                var camera = Root(scene, "Camera outside floor root").AddComponent<UnityEngine.Camera>();
                camera.transform.position = new Vector3(20f, 2f, 10f);
                var volume = Root(scene, "Horror fog").AddComponent<Volume>();
                horror = Root(scene, "Horror owner").AddComponent<HorrorDriver>();
                Set(horror, "_outputCamera", camera); Set(horror, "_fogVolume", volume);
                horror.Initialize(horrorConfig); horror.SetOwnerEnabled(true);
                horror.SetActiveEffects(new ActiveEffects(new[] { new ActiveEffect(new EffectId("afterglow"), EffectKind.Upgrade, 1) }));

                for (int generation = 0; generation < 3; generation++)
                {
                    Vector3 origin = new Vector3(54000f + generation * 100f, 0f, 54000f);
                    var bounds = new Bounds(origin + Vector3.up * 2f, new Vector3(12f, 4f, 12f));
                    var anchor = new LevelAnchor(1, 1, CakeAnchorType.Flow, origin);
                    var graph = LevelGraphUtility.Build(new[] { new LevelRoom(1, bounds.center, bounds.size) },
                        Array.Empty<LevelEdge>(), new[] { anchor }, 1, origin + Vector3.right * 4f);
                    floor.Initialize(graph, new[] { anchor });
                    floor.ApplyDestruction(new RoomDestructionSample(1, RoomPhase.Telegraph, .5f, 1f, .25f), 1f);
                    environment.AddRoom(1, bounds, true, false, Array.Empty<Vector3>(),
                        lightSockets: new[] { origin + new Vector3(0f, 2f, 5.8f) });
                    environment.SetExitFrame(1, origin, Quaternion.identity);
                    environment.SetOwnerEnabled(true); environment.SetObserver(origin); environment.Tick(1f);
                    var aim = new FlashlightSample(new EntityId(1), generation + 1, true,
                        origin + Vector3.up, Vector3.forward, 20f, 60f);
                    horror.SetFlashlight(aim); horror.SetAfterimage(aim, 2f);
                    horror.SetAfterglow(new InteractableState(1, InteractableKind.Light, 1, origin, InteractableStateValue.Inactive), 3f);
                    var old = floor.GetComponentsInChildren<LumenEffectPlayer>(true)
                        .Concat(environment.GetComponentsInChildren<LumenEffectPlayer>(true))
                        .Concat(horror.GetComponentsInChildren<LumenEffectPlayer>(true)).ToArray();
                    Assert.That(floor.GetComponentsInChildren<LumenEffectPlayer>(true).Length, Is.GreaterThanOrEqualTo(5));
                    Assert.That(environment.GetComponentsInChildren<LumenEffectPlayer>(true).Length, Is.GreaterThanOrEqualTo(2));
                    Assert.That(horror.GetComponentsInChildren<LumenEffectPlayer>(true).Length, Is.EqualTo(4));
                    Assert.That(horror.GetComponentsInChildren<LumenEffectPlayer>(true)
                        .Any(player => player.name == "Lumen 2 Dying Afterimage"), Is.True);
                    // Drivers normally suppress vendor rendering in Edit Mode. Deliberately
                    // enable it here to exercise the real manager/static-event lifecycle too.
                    foreach (var player in old) { player.gameObject.SetActive(true); player.enabled = true; }
                    AssertRegistered(old);
                    floor.RemovePickup(1, PickupKind.Cake); // Inactive pooled cake glows also belong to this floor.
                    environment.SetOwnerEnabled(false);
                    floor.Teardown(); environment.Teardown(); horror.ResetRound();
                    foreach (var player in old) Assert.That(player == null, Is.True, "Old floor Lumen component survived.");
                    AssertUnregistered(old);
                    var all = Resources.FindObjectsOfTypeAll<LumenEffectPlayer>();
                    Assert.That(all.Any(current => old.Any(prior => ReferenceEquals(current, prior))), Is.False);
                    var fresh = camera.GetComponentsInChildren<LumenEffectPlayer>(true);
                    Assert.That(fresh.Length, Is.EqualTo(2), "Reset builds a fresh camera-local rig, not the old world aim.");
                    Assert.That(fresh.All(player => player.transform.parent.localPosition == Vector3.zero), Is.True);
                    Assert.That(unrelatedPlayer != null && unrelatedPlayer.isActiveAndEnabled, Is.True);
                    Assert.That(JsonUtility.ToJson(profile), Is.EqualTo(profileBefore));
                }
                unrelated.Teardown();
            }
            finally
            {
                if (floor != null) floor.Teardown();
                if (environment != null) environment.Teardown();
                if (horror != null) horror.Teardown();
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var glow in root.GetComponentsInChildren<FloorLumenGlow>(true)) glow.Teardown();
                Unsupported.RestoreOverrideLightingSettings();
                EditorSceneManager.ClosePreviewScene(scene);
                if (floorConfig != null) Object.DestroyImmediate(floorConfig);
                if (environmentConfig != null) Object.DestroyImmediate(environmentConfig);
                if (horrorConfig != null) Object.DestroyImmediate(horrorConfig);
                if (profile != null) Object.DestroyImmediate(profile);
                if (clip != null) Object.DestroyImmediate(clip);
                foreach (var manager in Resources.FindObjectsOfTypeAll<LumenManager>())
                    if (!previousManagers.Contains(manager)) Object.DestroyImmediate(manager.gameObject);
            }
        }

        private static void AssertRegistered(LumenEffectPlayer[] players)
        {
            var registered = Resources.FindObjectsOfTypeAll<LumenManager>()
                .SelectMany(manager => ((IEnumerable)Get(manager, "players")).Cast<object>()).ToArray();
            foreach (var player in players) Assert.That(registered.Any(value => ReferenceEquals(value, player)), Is.True);
        }
        private static void AssertUnregistered(LumenEffectPlayer[] old)
        {
            foreach (var manager in Resources.FindObjectsOfTypeAll<LumenManager>())
                foreach (string field in new[] { "players", "playersSet" })
                    foreach (object value in (IEnumerable)Get(manager, field))
                        Assert.That(old.Any(player => ReferenceEquals(player, value)), Is.False, field);
            var redraw = (Delegate)typeof(LumenUtility).GetField("OnRedoEntireEffect", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
            foreach (var callback in redraw?.GetInvocationList() ?? Array.Empty<Delegate>())
                Assert.That(old.Any(player => ReferenceEquals(player, callback.Target)), Is.False, "Static redraw retained an old player.");
        }
        private static GameObject Root(Scene scene, string name)
        { var root = new GameObject(name); SceneManager.MoveGameObjectToScene(root, scene); return root; }
        private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
