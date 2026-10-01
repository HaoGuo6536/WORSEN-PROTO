// ============================================================================
// FogDoorwayPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies exact doorway confinement independent of a camera or density grid.
//   Both doorway axes and signs, clipped openings and footprint seams are covered.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Presentation · Fog.
// KEY RESPONSIBILITIES:
//   - Require a zero-depth plane inside the collapsing room on all four faces.
//   - Reject internal seams, missing openings and vertically separate portals.
// DEPENDENCIES:
//   - NUnit, Core and pure FogDoorwayPresenter.
// USAGE NOTES:
//   Headless-safe value tests; shaders and visibility require coordinator Unity QA.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Fog;
namespace Worsen.Tests.Fog
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class FogDoorwayPresenterTests
    {
        [TestCase(0, -1)] [TestCase(0, 1)] [TestCase(2, -1)] [TestCase(2, 1)]
        public void AllVerticesStayInDoorwayPlaneWithoutCorridorProtrusion(int axis, int side)
        {
            var bounds = new Bounds(new Vector3(0f, 2f, 0f), new Vector3(12f, 4f, 12f));
            var portal = Vector3.zero; portal[axis] = side * 6f;
            var room = new GeneratedRoomSample(1, bounds, false, false, new[] { portal });
            var vertices = FogDoorwayPresenter.Vertices(room, portal, 3f, 3f, .02f, .05f);
            Assert.That(vertices.Length, Is.EqualTo(4));
            foreach (var vertex in vertices)
            {
                Assert.That(vertex[axis], Is.EqualTo(side * 5.98f).Within(.00001f));
                Assert.That(vertex[2 - axis], Is.InRange(-1.5f, 1.5f));
                Assert.That(vertex.y, Is.InRange(0f, 3f));
                Assert.That(vertex[axis] * side, Is.LessThan(6f));
            }
        }

        [Test]
        public void OpeningClipsAtCellEdgesAndCeilingAndRejectsNonBoundaryPortals()
        {
            var bounds = new Bounds(Vector3.one, Vector3.one * 2f);
            var room = new GeneratedRoomSample(1, bounds, false, false, null);
            var points = FogDoorwayPresenter.Vertices(room, new Vector3(2f, 1f, 0f), 3f, 3f, .02f, .05f);
            Assert.That(points.Length, Is.EqualTo(4));
            foreach (var point in points)
            { Assert.That(point.z, Is.InRange(0f, 1.5f)); Assert.That(point.y, Is.InRange(1f, 2f)); }
            Assert.That(FogDoorwayPresenter.Vertices(room, Vector3.one, 3f, 3f, .02f, .05f), Is.Empty);
            Assert.That(FogDoorwayPresenter.Vertices(room, new Vector3(2f, 5f, 1f), 3f, 3f, .02f, .05f), Is.Empty);
        }

        [Test]
        public void FootprintSeamsAreRejectedRatherThanFilledWithFog()
        {
            var cells = new[] { new Bounds(new Vector3(0f, 2f, 0f), new Vector3(12f, 4f, 12f)),
                new Bounds(new Vector3(12f, 2f, 0f), new Vector3(12f, 4f, 12f)) };
            var room = new GeneratedRoomSample(1, new Bounds(new Vector3(6f, 2f, 0f), new Vector3(24f, 4f, 12f)),
                false, false, null, cells: cells);
            Assert.That(FogDoorwayPresenter.Vertices(room, new Vector3(6f, 0f, 0f), 3f, 3f, .02f, .05f), Is.Empty);
            Assert.That(FogDoorwayPresenter.Vertices(room, new Vector3(18f, 0f, 0f), 3f, 3f, .02f, .05f).Length, Is.EqualTo(4));
        }
    }
}
