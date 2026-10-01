// ============================================================================
// HunterHearingUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the explicit gameplay hearing allowlist independently of acoustic data.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Core · hearing policy.
// KEY RESPONSIBILITIES:
//   - Reject world, Pacification, presentation, false-positive and unknown origins.
//   - Admit movement, firecracker, player-triggered cake traps and Loud Keys only.
// DEPENDENCIES:
//   - NUnit, Core values and Unity value types only.
// USAGE NOTES:
//   Pure tests; no Audio, scene, engine timing or objects required.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Core
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HunterHearingUtilityTests
    {
        [TestCase(NoiseOrigin.PlayerMovement, true)]
        [TestCase(NoiseOrigin.Firecracker, true)]
        [TestCase(NoiseOrigin.PlayerTriggeredCakeTrap, true)]
        [TestCase(NoiseOrigin.LoudKeys, true)]
        [TestCase(NoiseOrigin.Pacification, false)]
        [TestCase(NoiseOrigin.World, false)]
        [TestCase(NoiseOrigin.Presentation, false)]
        [TestCase(NoiseOrigin.FalsePositive, false)]
        [TestCase(NoiseOrigin.Unspecified, false)]
        [TestCase((NoiseOrigin)999, false)]
        public void OriginAloneControlsAdmission(NoiseOrigin origin, bool allowed)
        {
            foreach (NoiseSourceKind kind in Enum.GetValues(typeof(NoiseSourceKind)))
                foreach (float loudness in new[] { 0f, 1000f })
                    foreach (EntityId source in new[] { EntityId.None, new EntityId(1), new EntityId(-1) })
                        Assert.That(HunterHearingUtility.Allows(new NoiseEvent(source, Vector3.zero, loudness, 1, kind, origin)), Is.EqualTo(allowed));
        }
        [Test] public void LegacyAcousticFirecrackerKindCannotGrantHearing()
        {
            var noise = new NoiseEvent(new EntityId(1), Vector3.zero, 1000f, 1, NoiseSourceKind.Firecracker);
            Assert.That(noise.Origin, Is.EqualTo(NoiseOrigin.Unspecified));
            Assert.That(HunterHearingUtility.Allows(noise), Is.False);
        }
        [Test] public void RunSummaryCannotBeConstructedWithEarlyBailMetadata()
        {
            foreach (var constructor in typeof(RunSummary).GetConstructors())
                foreach (var parameter in constructor.GetParameters()) Assert.That(parameter.Name, Is.Not.EqualTo("bailed"));
            Assert.That(typeof(RunSummary).GetProperty("Bailed"), Is.Null);
        }
    }
}
