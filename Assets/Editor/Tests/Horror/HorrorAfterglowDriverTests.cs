// ============================================================================
// HorrorAfterglowDriverTests.cs
// ============================================================================
// PURPOSE:
//   Exercises transient Afterglow lights without enabling the vendor renderer.
//   The test checks resource ownership, not visual quality or gameplay immunity.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Horror.
// KEY RESPONSIBILITIES:
//   - Verify authored profile preservation, expiry and effect-removal cleanup.
// DEPENDENCIES:
//   NUnit, Unity objects, Core, Horror and Lumen data types.
// USAGE NOTES:
//   Native Edit Mode fixture for the coordinator; no scene assets are saved.
// ============================================================================
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using DistantLands.Lumen;
using Worsen.Core;
using Worsen.Presentation.Horror;
namespace Worsen.Tests.Horror
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HorrorAfterglowDriverTests
    {
        [Test] public void ExpiryAndRemovalDestroyOnlyOwnedLightsWithoutMutatingTheProfile()
        {
            var owner = new GameObject("Afterglow test");
            var prefab = new GameObject("Authored fill"); prefab.SetActive(false);
            var profile = ScriptableObject.CreateInstance<LumenEffectProfile>();
            profile.layers.Add(new LumenLightLayer { range = 2f, intensity = .2f });
            prefab.AddComponent<LumenEffectPlayer>().profile = profile;
            var config = ScriptableObject.CreateInstance<HorrorDriverConfig>();
            typeof(HorrorDriverConfig).GetField("_lumenNearFillPrefab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, prefab);
            var driver = owner.AddComponent<HorrorAfterglowDriver>();
            try
            {
                string before = JsonUtility.ToJson(profile);
                driver.Initialize(config);
                driver.SetEffects(new ActiveEffects(new[] { new ActiveEffect(new EffectId("afterglow"), EffectKind.Upgrade, 1) }));
                var light = new InteractableState(1, InteractableKind.Light, 1, Vector3.one, InteractableStateValue.Inactive);
                driver.Observe(light, 3f);
                var output = owner.GetComponentInChildren<LumenEffectPlayer>(true);
                Assert.That(output, Is.Not.Null); Assert.That(output.enabled, Is.False);
                Assert.That(output.transform.position, Is.EqualTo(Vector3.one));
                Assert.That(output.brightness, Is.EqualTo(config.AfterglowStrength));
                driver.Tick(1.5f);
                Assert.That(output.brightness, Is.EqualTo(config.AfterglowStrength * .5f));
                driver.Tick(1.5f); Assert.That(output == null, Is.True);
                driver.Observe(light, 3f); Assert.That(owner.GetComponentsInChildren<LumenEffectPlayer>(true), Is.Empty);
                driver.Clear(); driver.Observe(light, 3f);
                driver.SetEffects(null); Assert.That(owner.GetComponentsInChildren<LumenEffectPlayer>(true), Is.Empty);
                Assert.That(JsonUtility.ToJson(profile), Is.EqualTo(before));
                Assert.That(prefab != null && !prefab.activeSelf, Is.True);
            }
            finally
            { driver.Clear(); Object.DestroyImmediate(owner); Object.DestroyImmediate(prefab); Object.DestroyImmediate(profile); Object.DestroyImmediate(config); }
        }
    }
}
