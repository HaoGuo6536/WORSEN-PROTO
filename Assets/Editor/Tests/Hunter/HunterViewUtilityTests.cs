// ============================================================================
// HunterViewUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Verifies camera-frustum math independently of a scene or actual Camera.
//   Pitch and look-back must affect observation rather than using body heading.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Exercise view edges, direct look, freshness and invalid samples.
// DEPENDENCIES:
//   - HunterViewUtility, Core values and NUnit.
// USAGE NOTES:
//   All camera values and ticks are injected.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HunterViewUtilityTests
    {
        [Test] public void CameraRotationPitchFrustumAndFreshnessAreRespected()
        {
            var view = new HunterPlayerView(Vector3.zero, Quaternion.identity, 90f, 60f, 7);
            Assert.That(HunterViewUtility.Fresh(view, 7), Is.True); Assert.That(HunterViewUtility.Fresh(view, 8), Is.False);
            Assert.That(HunterViewUtility.Fresh(default, 0), Is.False);
            Assert.That(HunterViewUtility.Contains(view, new Vector3(4, 0, 5)), Is.True);
            Assert.That(HunterViewUtility.Contains(view, new Vector3(4, 0, 5), 8f), Is.False);
            Assert.That(HunterViewUtility.Contains(view, new Vector3(0, 4, 5)), Is.False);
            Assert.That(HunterViewUtility.Contains(view, Vector3.back), Is.False);
            var back = new HunterPlayerView(Vector3.zero, Quaternion.Euler(-30, 180, 0), 90, 60, 7);
            Assert.That(HunterViewUtility.Contains(back, back.Rotation * Vector3.forward * 5), Is.True);
            Assert.That(HunterViewUtility.Contains(back, Vector3.forward * 5), Is.False);
        }
    }
}
