// ============================================================================
// HunterLightDriverTests.cs
// ============================================================================
// PURPOSE:
//   Verifies Hunter behavior with explicit reproducible fixtures.
//   Tests exercise observable light, physical attacks, route admission or creature
//   animation contracts without changing authored gameplay assets.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10), test suite (section 11) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Keep per-life state separate from shared configuration and foreign systems.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Coordinator runs Unity tests with the exclusive lease. Fixtures clean up their own objects.
// ============================================================================
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    public sealed class HunterLightDriverTests
    {
        [Test] public void WallBlocksDirectIlluminationAndSourceObservation()
        {
            Vector3 origin = new Vector3(2900, 120, 2900);
            var root = new GameObject("Light sensing fixture"); root.transform.position = origin;
            HunterMotorDriverConfig config = ScriptableObject.CreateInstance<HunterMotorDriverConfig>();
            var wall = new GameObject("Opaque light wall"); wall.transform.position = origin + new Vector3(0, 1.5f, 3);
            wall.AddComponent<BoxCollider>().size = new Vector3(20, 5, 0.3f);
            try
            {
                HunterDriver driver = root.AddComponent<HunterDriver>();
                var so = new SerializedObject(driver); so.FindProperty("_config").objectReferenceValue = config; so.ApplyModifiedPropertiesWithoutUndo(); driver.Initialize();
                var light = new FlashlightSample(new EntityId(1), 0, true, origin + new Vector3(0, 1.5f, 6), Vector3.back, 10, 40);
                Physics.SyncTransforms(); HunterLightObservation blocked = driver.ProbeLight(light, 0, 20, 110, 0);
                Assert.That(blocked.Illuminated, Is.False); Assert.That(blocked.Observed, Is.False);
                wall.SetActive(false); Physics.SyncTransforms();
                HunterLightObservation clear = driver.ProbeLight(light, 0, 20, 110, 0);
                Assert.That(clear.Illuminated, Is.True); Assert.That(clear.Observed, Is.True);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(wall); Object.DestroyImmediate(config); }
        }
    }
}
