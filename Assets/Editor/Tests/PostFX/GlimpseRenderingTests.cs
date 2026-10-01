// ============================================================================
// GlimpseRenderingTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the Glimpse timer reaches an owned volume rather than a shared asset.
//   Shader contract checks are kept separate from the still-required rendered review.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · PostFX.
// KEY RESPONSIBILITIES:
//   - Verify reveal, blindness suppression, reset and volume teardown.
//   - Guard depth-tested, non-depth-writing outline rendering and explicit layer admission.
// DEPENDENCIES:
//   NUnit, Unity volumes, Core and PostFX; source inspection uses the project path.
// USAGE NOTES:
//   Native Edit Mode fixture; no GPU or frame-budget pass is implied.
// ============================================================================
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Worsen.Core;
using Worsen.Presentation.PostFX;
namespace Worsen.Tests.PostFX
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class GlimpseRenderingTests
    {
        [Test] public void RevealReachesOwnedVolumeAndBlindnessResetAndTeardownSuppressIt()
        {
            var owner = new GameObject("Glimpse volume test");
            var config = ScriptableObject.CreateInstance<PostFXDriverConfig>();
            var driver = owner.AddComponent<PostFXDriver>(); driver.ConfigureForSetup(); driver.Initialize(config);
            try
            {
                var volume = owner.GetComponent<Volume>();
                var profile = volume.profile;
                Assert.That(profile.TryGet(out GlimpseVolume glimpse), Is.True);
                driver.SetActiveEffects(new ActiveEffects(new[] { new ActiveEffect(new EffectId("glimpse"), EffectKind.Upgrade, 1) }));
                driver.SetLookBack(true);
                typeof(PostFXDriver).GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(driver, null);
                Assert.That(glimpse.Strength.value, Is.EqualTo(1f));
                Assert.That(glimpse.Width.value, Is.EqualTo(config.GlimpseWidth));
                driver.SetBlindness(2f); Assert.That(glimpse.Strength.value, Is.Zero);
                driver.ResetEffects(); Assert.That(glimpse.Strength.value, Is.Zero);
                driver.Teardown(); Assert.That(glimpse == null && profile == null, Is.True);
                Assert.That(config != null, Is.True);
            }
            finally { driver.Teardown(); Object.DestroyImmediate(owner); Object.DestroyImmediate(config); }
        }
        [Test] public void OutlineUsesDepthWithoutWritingItAndNeverEnablesAllLayersByDefault()
        {
            string shader = File.ReadAllText(Path.Combine(Application.dataPath, "Shaders/GlimpseOutline.shader"));
            Assert.That(shader, Does.Contain("ZTest LEqual").And.Contain("ZWrite Off").And.Contain("Cull Front"));
            Assert.That(shader, Does.Not.Contain("ZTest Always"));
            var featureType = typeof(PostFXDriver).Assembly.GetType("Worsen.Presentation.PostFX.GlimpseRendererFeature", true);
            var feature = ScriptableObject.CreateInstance(featureType);
            try
            {
                var layers = (LayerMask)featureType.GetField("_hunterLayers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(feature);
                Assert.That(layers.value, Is.Zero);
            }
            finally { Object.DestroyImmediate(feature); }
        }
    }
}
