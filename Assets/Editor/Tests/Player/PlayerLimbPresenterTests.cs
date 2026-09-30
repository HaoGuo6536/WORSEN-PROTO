// ============================================================================
// PlayerLimbPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Checks first-person hand placement without a camera or scene. The authored
//   offset stays unchanged unless the supplied near plane would cut the hand.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Verify mirrored offsets and whole-hand near-plane clearance.
// DEPENDENCIES:
//   - PlayerLimbPresenter, NUnit and UnityEngine value types only.
// USAGE NOTES:
//   Pure Edit Mode tests. PlayerDriverTests covers the actual render callback;
//   neither fixture replaces owner review of the placeholder art in play.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Player;

namespace Worsen.Tests.Player
{
    public sealed class PlayerLimbPresenterTests
    {
        [TestCase(true)] [TestCase(false)]
        public void OrdinaryHandsKeepTheAuthoredLowerViewOffset(bool left)
        {
            Vector3 offset = new PlayerLimbPresenter().HandOffset(new Vector3(0.32f, -0.25f, 0.5f), left, 0.05f, 0.24f);
            Assert.That(offset, Is.EqualTo(new Vector3(left ? -0.32f : 0.32f, -0.25f, 0.5f)));
        }

        [TestCase(0.05f, 0.24f)] [TestCase(0.3f, 0.24f)] [TestCase(1f, 0.5f)]
        public void UnsafeDepthMovesTheWholeHandBeyondTheNearPlane(float near, float radius)
        {
            Vector3 offset = new PlayerLimbPresenter().HandOffset(new Vector3(0.32f, -0.25f, 0f), false, near, radius);
            Assert.That(offset.z - radius, Is.GreaterThan(near));
            Assert.That(offset.x, Is.EqualTo(0.32f));
            Assert.That(offset.y, Is.EqualTo(-0.25f));
        }
    }
}
