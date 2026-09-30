// ============================================================================
// PlayerLimbPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Checks first-person hand placement without a camera or scene. The authored
//   offset stays unchanged unless the supplied near plane would cut the hand.
//   Off-centre arm bounds are checked across pitched and reversed camera axes.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Verify mirrored offsets, projected bounds and whole-hand near-plane clearance.
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
        [TestCase(-85f, 0f)] [TestCase(0f, 180f)] [TestCase(85f, 180f)]
        public void OffCentreArmBoundsClearThePlaneAtEveryCorner(float pitch, float yaw)
        {
            var presenter = new PlayerLimbPresenter();
            Vector3 forward = Quaternion.Euler(pitch, yaw, 6f) * Vector3.forward;
            var bounds = new Bounds(new Vector3(0.12f, -0.3f, -0.2f), new Vector3(0.2f, 0.6f, 0.7f));
            float rear = presenter.RearExtent(bounds.center, bounds.extents, forward);
            Vector3 offset = presenter.HandOffset(new Vector3(0.32f, -0.25f, 0f), true, 0.3f, rear);
            foreach (float x in new[] { -1f, 1f })
            foreach (float y in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                Assert.That(offset.z + Vector3.Dot(corner, forward), Is.GreaterThan(0.3f));
            }
        }

        [Test]
        public void RearExtentIncludesTheArmsOffsetFromItsHandRoot()
        {
            Assert.That(new PlayerLimbPresenter().RearExtent(new Vector3(0f, -0.2f, -0.4f),
                new Vector3(0.1f, 0.2f, 0.3f), Vector3.forward), Is.EqualTo(0.7f).Within(0.00001f));
        }

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
