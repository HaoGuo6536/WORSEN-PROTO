// ============================================================================
// PostFXDamageVolumeTests.cs
// ============================================================================
// PURPOSE:
//   Checks real serialized defaults and the owned URP vignette override in Edit Mode.
//   These native checks deliberately do not claim GPU rendering or audible heartbeat
//   synchronization; the coordinator must inspect those in the integrated scene.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · PostFX.
// KEY RESPONSIBILITIES:
//   - Verify red border parameters, neutral full-screen color and independent blindness.
//   - Verify health/heartbeat manager forwarding and reset/teardown ownership.
// DEPENDENCIES:
//   PostFX, NUnit, UnityEngine and reflection for URP package parameter inspection.
// USAGE NOTES:
//   Native Edit Mode only. Explicitly tears down the Manager before destroying its
//   GameObject because Edit Mode does not guarantee MonoBehaviour lifecycle callbacks.
// ============================================================================
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Presentation.PostFX;

namespace Worsen.Tests.PostFX
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class PostFXDamageVolumeTests
    {
        [Test] public void RedBorderReachesOwnedVolumeWithoutFullScreenDimming()
        {
            var owner = new GameObject("Damage border native fixture"); owner.SetActive(false);
            var config = ScriptableObject.CreateInstance<PostFXDriverConfig>();
            var driver = owner.AddComponent<PostFXDriver>(); driver.ConfigureForSetup();
            var manager = owner.AddComponent<PostFXManager>();
            Set(manager, "_driver", driver); Set(manager, "_config", config);
            try
            {
                manager.Initialize();
                Assert.That(config.DamageFadeSeconds, Is.EqualTo(1.25f));
                Assert.That(config.DamageVignettePeak, Is.EqualTo(.5f));
                Assert.That(config.LowHealthFraction, Is.EqualTo(.35f));
                Assert.That(config.LowHealthVignette, Is.EqualTo(.25f));
                Assert.That(config.LowHealthPulseMultiplier, Is.EqualTo(.5f));
                Assert.That(config.DamageFullStrengthHealthFraction, Is.EqualTo(.5f));
                var state = (PostFXDriverState)Get(driver, "_state");
                var presenter = new PostFXPresenter();
                manager.SetInjury(100f, 100f); manager.SetInjury(75f, 100f);
                presenter.Tick(state, config, 0f); Apply(driver);
                object vignette = Get(driver, "_vignette");
                Assert.That(Value(vignette, "intensity"), Is.EqualTo(.25f));
                Assert.That(Value(vignette, "color"), Is.EqualTo(new Color(1f, .015f, .01f, 1f)));
                Assert.That(Value(vignette, "center"), Is.EqualTo(new Vector2(.5f, .5f)));
                Assert.That(Value(vignette, "smoothness"), Is.EqualTo(.3f));
                Assert.That(Value(vignette, "rounded"), Is.EqualTo(false));
                foreach (string parameter in new[] { "intensity", "color", "center", "smoothness", "rounded" })
                    Assert.That(Get(Get(vignette, parameter), "overrideState"), Is.EqualTo(true));
                object color = Get(driver, "_color");
                Assert.That(Value(color, "saturation"), Is.EqualTo(0f));
                Assert.That(Value(color, "postExposure"), Is.EqualTo(0f));
                Assert.That(Get(Get(color, "colorFilter"), "overrideState"), Is.EqualTo(false));
                Assert.That(Get(Get(color, "postExposure"), "overrideState"), Is.EqualTo(false));
                presenter.Tick(state, config, 1.25f); Apply(driver);
                Assert.That(Value(vignette, "intensity"), Is.EqualTo(0f));
                manager.SetInjury(25f, 100f); presenter.Tick(state, config, 2f); Apply(driver);
                float faint = (float)Value(vignette, "intensity");
                manager.SetHeartbeatEnvelope(1f); presenter.Tick(state, config, 0f); Apply(driver);
                Assert.That((float)Value(vignette, "intensity"), Is.EqualTo(faint * 1.5f).Within(.00001f));
                manager.SetBlindness(2f);
                Assert.That(config.BlindnessDarkness, Is.EqualTo(.85f).And.LessThan(1f));
                Assert.That(config.BlindnessOnsetSeconds, Is.EqualTo(.08f));
                Assert.That(config.BlindnessRecoverySeconds, Is.EqualTo(.65f));
                Assert.That(config.BlindnessVignette, Is.EqualTo(.75f));
                Assert.That(config.BlindnessVignetteRadius, Is.EqualTo(1f));
                Assert.That(config.BlindnessVignetteSoftness, Is.EqualTo(.6f));
                Assert.That(config.BlindnessBlurRadius, Is.EqualTo(1.5f));
                Assert.That(config.BlindnessEdgeBlurPixels, Is.EqualTo(4f));
                Assert.That(Value(color, "colorFilter"), Is.EqualTo(Color.white), "Onset requires elapsed time.");
                presenter.Tick(state, config, config.BlindnessOnsetSeconds); Apply(driver);
                Color blindTint = (Color)Value(color, "colorFilter");
                Assert.That(blindTint.r, Is.EqualTo(Mathf.LinearToGammaSpace(1f - config.BlindnessDarkness)).Within(.0001f));
                Assert.That(Value(Get(driver, "_frame"), "Lens"), Is.EqualTo(new Vector4(.75f, 1f, .6f, 4f)));
                Assert.That(Get(Get(driver, "_blur"), "active"), Is.EqualTo(true));
                Assert.That(Value(Get(driver, "_blur"), "gaussianMaxRadius"), Is.EqualTo(1.5f));
                Assert.That((float)Value(vignette, "intensity"), Is.GreaterThan(0f));
                manager.SetInjury(100f, 100f); presenter.Tick(state, config, 0f); Apply(driver);
                Assert.That(Value(vignette, "intensity"), Is.EqualTo(0f));
                Assert.That(Value(color, "colorFilter"), Is.EqualTo(blindTint));
                manager.ResetEffects(); Assert.That(Value(vignette, "intensity"), Is.EqualTo(0f));
                var profile = (Object)Get(driver, "_profile");
                manager.Teardown(); Assert.That(profile == null, Is.True);
                Assert.That((Object)vignette == null, Is.True);
            }
            finally { manager.Teardown(); Object.DestroyImmediate(owner); Object.DestroyImmediate(config); }
        }
        private static void Apply(PostFXDriver driver) => typeof(PostFXDriver)
            .GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(driver, null);
        private static object Value(object target, string name) => Get(Get(target, name), "value");
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static object Get(object target, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            return target.GetType().GetField(name, flags)?.GetValue(target)
                ?? target.GetType().GetProperty(name, flags).GetValue(target);
        }
    }
}
