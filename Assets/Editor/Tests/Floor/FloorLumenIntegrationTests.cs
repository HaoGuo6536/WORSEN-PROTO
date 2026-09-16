// ============================================================================
// FloorLumenIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Verifies that both exit styles and collapse warnings use native Lumen effects
//   without producing real Unity lights. Warning pulses and opening colors remain
//   driven by the Floor clock, and disable/re-enable must cleanly restore effects.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Domain · Floor native lighting integration.
// KEY RESPONSIBILITIES:
//   - Exercise native fake-light profiles through FloorDriver's public lifecycle.
//   - Check visibility, brightness, source-profile preservation and teardown.
// DEPENDENCIES:
//   NUnit, UnityEditor asset reads, UnityEngine, Core/Floor and DistantLands.Lumen.
// USAGE NOTES:
//   Uses temporary objects and a private imported-profile copy; writes no assets.
//   Existing Lumen managers are borrowed untouched; only newly created managers
//   are removed after owned players are destroyed. No rendering result is inferred.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using DistantLands.Lumen;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Floor
{
    public sealed class FloorLumenIntegrationTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void NativeRoomAndExitEffectsPreserveTimingWithoutAnyRealLights(bool physicalDoor)
        {
            var previousManagers = Object.FindObjectsByType<LumenManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject root = null, prefab = null;
            FloorDriver driver = null;
            FloorDriverConfig config = null;
            LumenEffectProfile profile = null;
            try
            {
                Assert.That(Application.isPlaying, Is.False);
                var imported = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.distantlands.lumen/Content/Prefabs/Lantern Effect.prefab");
                Assert.That(imported, Is.Not.Null);
                var sourcePlayer = imported.GetComponentInChildren<LumenEffectPlayer>(true);
                Assert.That(sourcePlayer, Is.Not.Null);
                string importedBefore = JsonUtility.ToJson(sourcePlayer.profile);
                profile = Object.Instantiate(sourcePlayer.profile);
                profile.autoAssignSun = false;
                profile.layers.RemoveAll(layer => !(layer is LumenLightLayer));
                Assert.That(profile.layers.Count, Is.GreaterThan(0));
                foreach (LumenLightLayer layer in profile.layers)
                { layer.range = 2f; layer.color = Color.white; layer.isSpotlight = false; layer.fluctuation = false; layer.intensity = 0.2f; }
                string privateBefore = JsonUtility.ToJson(profile);
                prefab = new GameObject("Private Floor Lumen Prefab"); prefab.SetActive(false);
                prefab.AddComponent<LumenEffectPlayer>().profile = profile;
                Assert.That(prefab.GetComponentsInChildren<UnityEngine.Light>(true), Is.Empty);
                config = ScriptableObject.CreateInstance<FloorDriverConfig>();
                Set(config, "_lumenRoomWarningPrefab", prefab); Set(config, "_lumenExitGlowPrefab", prefab);
                Set(config, "_usePhysicalExitDoor", physicalDoor);
                root = new GameObject("Native Floor Lumen Fixture");
                driver = root.AddComponent<FloorDriver>(); Set(driver, "_config", config);
                Vector3 origin = new Vector3(54000f, 0f, 54000f);
                var graph = LevelGraphUtility.Build(new[] { new LevelRoom(1, origin + Vector3.up * 2f, new Vector3(12f, 4f, 12f)) },
                    Array.Empty<LevelEdge>(), new[] { new LevelAnchor(1, 1, CakeAnchorType.Flow, origin + Vector3.left * 3f) }, 1, origin);
                driver.Initialize(graph, Array.Empty<LevelAnchor>());
                Assert.That(root.GetComponentsInChildren<UnityEngine.Light>(true), Is.Empty);
                var room = root.GetComponentInChildren<RoomCollapseVolume>();
                var warning = room.GetComponentInChildren<LumenEffectPlayer>(true);
                Assert.That(warning, Is.Not.Null);
                Assert.That(warning.gameObject.activeInHierarchy, Is.False);
                var exit = root.GetComponentsInChildren<LumenEffectPlayer>(true).Single(item => item != warning);
                Assert.That(exit.isActiveAndEnabled, Is.True);
                Assert.That(exit.color, Is.EqualTo(config.ExitLockedColor));
                Assert.That(exit.brightness, Is.EqualTo(0.3f));
                Assert.That(exit.updateFrequency, Is.EqualTo(LumenEffectPlayer.UpdateFrequency.ViaScripting));
                driver.ApplyRoomPhase(1, RoomPhase.Telegraph);
                driver.TickWarnings(0.15f);
                Assert.That(warning.isActiveAndEnabled, Is.True);
                Assert.That(warning.color, Is.EqualTo(config.WarningColor));
                float expected = new FloorPresenter().WarningIntensity(0.15f, config.WarningPulsePeriod, config.WarningIntensity);
                Assert.That(warning.brightness, Is.EqualTo(expected).Within(0.0001f));
                Assert.That(warning.transform.childCount, Is.GreaterThan(0), "Native fake layers are instantiated when visible.");
                driver.ApplyRoomPhase(1, RoomPhase.Encroaching);
                Assert.That(warning.gameObject.activeInHierarchy, Is.False);
                Assert.That(warning.transform.childCount, Is.Zero, "Disabling releases vendor-generated meshes.");
                driver.OpenExit(Array.Empty<LevelAnchor>());
                if (physicalDoor)
                {
                    driver.TickWarnings(0.15f + config.ExitDoorOpeningDuration * 0.5f);
                    Assert.That(root.GetComponentInChildren<FloorExitDoor>().FullyOpen, Is.False);
                    Assert.That(exit.brightness, Is.EqualTo(1.4f).Within(0.0001f));
                    driver.TickWarnings(0.15f + config.ExitDoorOpeningDuration);
                    Assert.That(root.GetComponentInChildren<FloorExitDoor>().FullyOpen, Is.True);
                    Assert.That(exit.brightness, Is.EqualTo(2.5f).Within(0.0001f));
                }
                else Assert.That(exit.brightness, Is.EqualTo(config.WarningIntensity));
                Assert.That(exit.color, Is.EqualTo(config.ExitOpenColor));
                root.SetActive(false);
                Assert.That(exit.transform.childCount, Is.Zero);
                root.SetActive(true);
                Assert.That(exit.isActiveAndEnabled, Is.True);
                Assert.That(exit.transform.childCount, Is.GreaterThan(0));
                Assert.That(warning.gameObject.activeInHierarchy, Is.False, "An encroached room must stay unlit after re-enable.");
                Assert.That(root.GetComponentsInChildren<UnityEngine.Light>(true), Is.Empty);
                Assert.That(JsonUtility.ToJson(sourcePlayer.profile), Is.EqualTo(importedBefore));
                Assert.That(JsonUtility.ToJson(profile), Is.EqualTo(privateBefore));
                driver.Teardown();
                Assert.That(root.GetComponentsInChildren<LumenEffectPlayer>(true), Is.Empty);
            }
            finally
            {
                if (driver != null) driver.Teardown();
                if (root != null) Object.DestroyImmediate(root);
                if (prefab != null) Object.DestroyImmediate(prefab);
                if (config != null) Object.DestroyImmediate(config);
                if (profile != null) Object.DestroyImmediate(profile);
                foreach (var manager in Object.FindObjectsByType<LumenManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (!previousManagers.Contains(manager)) Object.DestroyImmediate(manager.gameObject);
            }
        }
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
