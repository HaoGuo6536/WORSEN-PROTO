// ============================================================================
// ConsumableAimAdmissionTests.cs
// ============================================================================
// PURPOSE:
//   Proves face-targeted consumables use the injected camera aim, independently of
//   whether the flashlight is enabled. Cosmetic item sway never supplies another
//   ray, and invalid or occluded faces must fail admission without a committed use.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · HorrorEffects.
// KEY RESPONSIBILITIES:
//   - Verify camera aim, visibility and beam independence for vial admission.
// DEPENDENCIES:
//   NUnit, Core and HorrorEffects pure controller/config values only.
// USAGE NOTES:
//   Managed config shell with explicit values; no native object construction.
// ============================================================================
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.HorrorEffects;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.HorrorEffects
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ConsumableAimAdmissionTests
    {
        [TestCase(false)] [TestCase(true)]
        public void VialAdmissionUsesCameraAimWithBeamOnOrOff(bool beam)
        {
            var config = (HorrorEffectsConfig)FormatterServices.GetUninitializedObject(typeof(HorrorEffectsConfig));
            typeof(HorrorEffectsConfig).GetField("vialRange", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, 4f);
            typeof(HorrorEffectsConfig).GetField("vialCone", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, 45f);
            var items = new ConsumableController(new ConsumableBehaviorState(), config); items.BeginFloor();
            var face = new HunterFaceSample(new EntityId(-1), Vector3.forward * 3f, Vector3.zero, true);
            var aim = new FlashlightSample(new EntityId(1), 1, beam, Vector3.zero, Vector3.forward, 18f, 52f);
            Assert.That(items.CanUse("glass-vial", 100f, 100f, aim, new[] { face }, null, Vector3.zero, out var target, out _), Is.True);
            Assert.That(target, Is.EqualTo(face.HunterId)); Assert.That(items.DrainStuns(), Is.Empty, "Admission alone is not a use.");
            var away = new FlashlightSample(new EntityId(1), 1, beam, Vector3.zero, Vector3.right, 18f, 52f);
            Assert.That(items.CanUse("glass-vial", 100f, 100f, away, new[] { face }, null, Vector3.zero, out _, out _), Is.False);
            Assert.That(items.CanUse("glass-vial", 100f, 100f, aim,
                new[] { new HunterFaceSample(face.HunterId, face.Head, face.Feet, false) }, null, Vector3.zero, out _, out _), Is.False);
            Assert.That(items.CanUse("glass-vial", 0f, 100f, aim, new[] { face }, null, Vector3.zero, out _, out _), Is.False);
        }
    }
}
