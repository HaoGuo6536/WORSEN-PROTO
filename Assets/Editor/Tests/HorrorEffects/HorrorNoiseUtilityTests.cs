// ============================================================================
// HorrorNoiseUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Checks the temporary producer-origin adapter independently of live hearing.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · HorrorEffects.
// KEY RESPONSIBILITIES:
//   - Admit firecracker and player-triggered cake traps, reject world/shrine noise.
// DEPENDENCIES:
//   NUnit, Core values and HorrorEffects pure utility only.
// USAGE NOTES:
//   Managed pure tests. Run/Director ingress needs coordinator integration tests.
// ============================================================================
using NUnit.Framework;
using Worsen.Core;
using Worsen.Session.HorrorEffects;
namespace Worsen.Tests.HorrorEffects
{
    public sealed class HorrorNoiseUtilityTests
    {
        [TestCase(NoiseSourceKind.Firecracker, 0, HorrorNoiseOrigin.Firecracker, true)]
        [TestCase(NoiseSourceKind.Trap, 1, HorrorNoiseOrigin.PlayerTriggeredCakeTrap, true)]
        [TestCase(NoiseSourceKind.Trap, 0, HorrorNoiseOrigin.World, false)]
        [TestCase(NoiseSourceKind.Shrine, 0, HorrorNoiseOrigin.Pacification, false)]
        [TestCase(NoiseSourceKind.Shrine, 1, HorrorNoiseOrigin.Pacification, false)]
        [TestCase(NoiseSourceKind.Door, 1, HorrorNoiseOrigin.World, false)]
        [TestCase(NoiseSourceKind.CakePickup, 1, HorrorNoiseOrigin.World, false)]
        [TestCase(NoiseSourceKind.Other, 1, HorrorNoiseOrigin.World, false)]
        public void OnlyExplicitGameplayProducersReachHunters(NoiseSourceKind kind, int source,
            HorrorNoiseOrigin expected, bool audible)
        {
            var noise = new NoiseEvent(new EntityId(source), default, 20f, 1, kind);
            Assert.That(HorrorNoiseUtility.Origin(noise), Is.EqualTo(expected));
            Assert.That(HorrorNoiseUtility.HunterAudible(noise), Is.EqualTo(audible));
            var fact = HorrorNoiseUtility.Annotate(noise);
            Assert.That(fact.Origin, Is.EqualTo(expected));
            Assert.That(HorrorNoiseUtility.HunterAudible(fact), Is.EqualTo(audible));
            Assert.That(HorrorNoiseUtility.HunterAudible(new HorrorNoiseFact(noise, HorrorNoiseOrigin.World)), Is.False,
                "World provenance cannot be promoted by its acoustic category.");
        }
    }
}
