// ============================================================================
// FloorBoundsUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Regresses inclusive managed room-cell occupancy without native engine calls.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Floor.
// KEY RESPONSIBILITIES:
//   - Check all faces/corners, exterior points, negative extents and invalid input.
// DEPENDENCIES:
//   - Floor utility, UnityEngine value types and NUnit only.
// USAGE NOTES:
//   Pure Edit Mode; no engine objects or Bounds.Contains invocation.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Floor;
namespace Worsen.Tests.Floor
{
    public sealed class FloorBoundsUtilityTests
    {
        [TestCase(1, 0, 0)] [TestCase(-1, 0, 0)]
        [TestCase(0, 1, 0)] [TestCase(0, -1, 0)]
        [TestCase(0, 0, 1)] [TestCase(0, 0, -1)]
        public void EveryFaceIsInclusiveAndSlightlyOutsideIsRejected(int x, int y, int z)
        {
            var cell = new Bounds(new Vector3(12f, 3f, -4f), new Vector3(8f, 6f, 10f));
            var point = cell.center + Vector3.Scale(cell.extents, new Vector3(x, y, z));
            Assert.That(FloorBoundsUtility.Contains(cell, point), Is.True);
            Assert.That(FloorBoundsUtility.Contains(cell, point + new Vector3(x, y, z) * .01f), Is.False);
        }
        [Test]
        public void EveryCornerAndCenterAreInside()
        {
            var cell = new Bounds(Vector3.one, Vector3.one * 2f);
            Assert.That(FloorBoundsUtility.Contains(cell, cell.center), Is.True);
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                Assert.That(FloorBoundsUtility.Contains(cell, cell.center + new Vector3(x, y, z)), Is.True);
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void NonfinitePointIsRejectedOnEveryAxis(float value)
        {
            var cell = new Bounds(Vector3.zero, Vector3.one * 4f);
            Assert.That(FloorBoundsUtility.Contains(cell, new Vector3(value, 0f, 0f)), Is.False);
            Assert.That(FloorBoundsUtility.Contains(cell, new Vector3(0f, value, 0f)), Is.False);
            Assert.That(FloorBoundsUtility.Contains(cell, new Vector3(0f, 0f, value)), Is.False);
        }
        [Test]
        public void NegativeExtentCannotContainAndZeroExtentContainsOnlyItsCenter()
        {
            Assert.That(FloorBoundsUtility.Contains(new Bounds(Vector3.zero, new Vector3(-1f, 1f, 1f)), Vector3.zero), Is.False);
            Assert.That(FloorBoundsUtility.Contains(new Bounds(Vector3.one, Vector3.zero), Vector3.one), Is.True);
            Assert.That(FloorBoundsUtility.Contains(new Bounds(Vector3.one, Vector3.zero), Vector3.one + Vector3.up), Is.False);
        }
    }
}
