// ============================================================================
// WeaverPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies ceiling-relative body height and stable firing probe ordering.
//   These calculations use only values, including rooms on elevated storeys.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Check room heights, floor drops, low ceilings and deterministic candidates.
// DEPENDENCIES:
//   - WeaverPresenter, UnityEngine values and NUnit only.
// USAGE NOTES:
//   No native scene or physics operations.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Hunter.Archetypes.Weaver;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class WeaverPresenterTests
    {
        [TestCase(0f, 5f, false, 3.05f)]
        [TestCase(10f, 17f, false, 5.05f)]
        [TestCase(0f, 1f, false, 0f)]
        [TestCase(10f, 17f, true, 0f)]
        public void BodyFitsCurrentRoomAndDropsToFloor(float floor, float ceiling, bool drop, float expected)
            => Assert.That(new WeaverPresenter().CeilingOffset(floor, ceiling, 1.8f, .15f, drop), Is.EqualTo(expected).Within(.00001f));
        [Test] public void CandidateRingIsStableAndKeepsTheFloorPlane()
        {
            var presenter = new WeaverPresenter(); var origin = new Vector3(4, 10, 6);
            for (int i = 0; i < 8; i++)
            {
                Vector3 candidate = presenter.Candidate(origin, i, 8, 2.5f);
                Assert.That(candidate, Is.EqualTo(presenter.Candidate(origin, i, 8, 2.5f)));
                Assert.That(candidate.y, Is.EqualTo(origin.y));
                Assert.That(Vector3.Distance(origin, candidate), Is.EqualTo(2.5f).Within(.00001f));
            }
        }
    }
}
