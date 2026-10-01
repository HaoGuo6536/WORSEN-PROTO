// ============================================================================
// EnvironmentPlacementPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Reproduces off-centre meshes and verifies their solved contact planes.
//   These tests exercise real placement math without active renderers or Unity
//   objects, including rejected fits and source positions on visible fixtures.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests (§11) · CastleEnvironment.
// KEY RESPONSIBILITIES:
//   - Check grounded bottoms, wall backs and ceiling fixture contact.
//   - Check finite scale bounds, long-axis alignment and transformed child bounds.
//   - Preserve template sockets without admitting any supplemental furniture.
// DEPENDENCIES:
//   - NUnit, Environment presenters and Unity value types only.
// USAGE NOTES:
//   Headless pure tier; no Quaternion.Euler or native object construction.
// ============================================================================
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Presentation.Environment;

namespace Worsen.Tests.CastleEnvironment
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class EnvironmentPlacementPresenterTests
    {
        [TestCase(.75f)] [TestCase(1f)] [TestCase(1.25f)]
        public void OffCentreFloorMeshBottomAndBackTouchEnvelope(float scale)
        {
            var mesh = new Bounds(new Vector3(3f, -2f, 4f), new Vector3(1.8f, .8f, .6f));
            var envelope = new Vector3(2.5f, 2.4f, 1.1f);
            var offset = EnvironmentPlacementPresenter.PlacementOffset(mesh, envelope, scale, true, false);
            Assert.That(mesh.min.y * scale + offset.y, Is.EqualTo(-envelope.y * .5f).Within(.00001f));
            Assert.That(mesh.min.z * scale + offset.z, Is.EqualTo(-envelope.z * .5f).Within(.00001f));
            Assert.That(mesh.center.x * scale + offset.x, Is.Zero.Within(.00001f));
        }

        [TestCase(0f)] [TestCase(90f)] [TestCase(180f)] [TestCase(270f)] [TestCase(37f)]
        public void WallBackIsFlushAndFrontFacesIntoRoomAtEveryYaw(float yaw)
        {
            var mesh = new Bounds(new Vector3(-2f, 5f, -4f), new Vector3(1.2f, 2.5f, .1f));
            var envelope = new Vector3(2f, 2.8f, .6f);
            var offset = EnvironmentPlacementPresenter.PlacementOffset(mesh, envelope, 1f, false, false);
            var origin = new Vector3(10f, 4f, -5f);
            var normal = EnvironmentPlacementPresenter.RotateYaw(Vector3.forward, yaw);
            var back = origin + EnvironmentPlacementPresenter.RotateYaw(new Vector3(mesh.center.x, mesh.center.y, mesh.min.z) + offset, yaw);
            Assert.That(Vector3.Dot(back - origin, normal), Is.EqualTo(-.3f).Within(.00001f));
            var front = origin + EnvironmentPlacementPresenter.RotateYaw(new Vector3(mesh.center.x, mesh.center.y, mesh.max.z) + offset, yaw);
            Assert.That(Vector3.Dot(front - back, normal), Is.EqualTo(.1f).Within(.00001f));
        }

        [TestCase(.1f, 1.25f)] [TestCase(1f, 1f)] [TestCase(1.25f, .8f)] [TestCase(2f, 0f)]
        [TestCase(0f, 0f)] [TestCase(float.NaN, 0f)] [TestCase(float.PositiveInfinity, 0f)]
        public void FitAllowsBoundedGrowthButRejectsAbsurdOrInvalidSizes(float size, float expected)
            => Assert.That(EnvironmentPlacementPresenter.FitScale(Vector3.one * size, Vector3.one, .75f, 1.25f),
                Is.EqualTo(expected).Within(.00001f));

        [Test]
        public void InvalidEnvelopeAndScaleRangeRejectRatherThanClampIntoAVisual()
        {
            Assert.That(EnvironmentPlacementPresenter.FitScale(Vector3.one, Vector3.zero, .75f, 1.25f), Is.Zero);
            Assert.That(EnvironmentPlacementPresenter.FitScale(Vector3.one, Vector3.one, 2f, 1f), Is.Zero);
            Assert.That(EnvironmentPlacementPresenter.FitScale(Vector3.one, Vector3.one, float.NaN, 1f), Is.Zero);
        }

        [Test]
        public void ChestAndBannerLongAxesFaceTheWallButSevenMetreArchIsRejected()
        {
            var chest = new Bounds(Vector3.zero, new Vector3(.86f, .83f, 1.96f));
            var aligned = EnvironmentPlacementPresenter.RotateBounds(chest, EnvironmentPlacementPresenter.LongAxisYaw(chest.size));
            float scale = EnvironmentPlacementPresenter.FitScale(aligned.size, EnvironmentDriverConfig.DefaultFloorEnvelope, .75f, 1.25f);
            Assert.That(scale, Is.InRange(.75f, 1.25f));
            Assert.That(aligned.size.x, Is.EqualTo(1.96f).Within(.00001f));
            var banner = new Bounds(Vector3.zero, new Vector3(.04f, 2.73f, .89f));
            var facing = EnvironmentPlacementPresenter.RotateBounds(banner, EnvironmentPlacementPresenter.LongAxisYaw(banner.size));
            Assert.That(facing.size.z, Is.EqualTo(.04f).Within(.00001f));
            Assert.That(EnvironmentPlacementPresenter.FitScale(new Vector3(7f, 2f, .6f), new Vector3(2f, 2f, .6f), .75f, 1.25f), Is.Zero);
        }

        [Test]
        public void MeshBoundsIncludeNegativeChildScaleAndTranslation()
        {
            var matrix = Matrix4x4.identity; matrix.m00 = -2f; matrix.m11 = 3f; matrix.m22 = .5f;
            matrix.m03 = 8f; matrix.m13 = -2f; matrix.m23 = 4f;
            var result = EnvironmentPlacementPresenter.TransformBounds(new Bounds(Vector3.one, new Vector3(2f, 4f, 6f)), matrix);
            Assert.That(result.center, Is.EqualTo(new Vector3(6f, 1f, 4.5f)));
            Assert.That(result.size, Is.EqualTo(new Vector3(4f, 12f, 3f)));
        }

        [Test]
        public void CeilingFixtureTopContactsCeilingAndSourceTouchesItsUnderside()
        {
            var envelope = new Vector3(2f, 1.2f, 1.2f);
            var slot = EnvironmentPlacementPresenter.CeilingFixtureSlot(new Vector3(4f, 2.6f, 8f), 3.6f, envelope);
            var mesh = new Bounds(new Vector3(1f, 2f, 3f), new Vector3(.98f, .14f, .98f));
            var offset = EnvironmentPlacementPresenter.PlacementOffset(mesh, envelope, 1f, false, true);
            var placed = new Bounds(slot.Position + offset + mesh.center, mesh.size);
            Assert.That(placed.max.y, Is.EqualTo(3.6f).Within(.00001f));
            var source = EnvironmentPlacementPresenter.FixtureSource(placed, true);
            Assert.That(source.y, Is.EqualTo(placed.min.y));
            Assert.That(source.x, Is.EqualTo(4f)); Assert.That(source.z, Is.EqualTo(8f));
        }

        [TestCase(false)] [TestCase(true)]
        public void PolygonWallFixtureUsesTheNearestFaceAndInwardNormal(bool reverse)
        {
            var polygon = new[] { new Vector3(-6f, 0f, -4f), new Vector3(6f, 0f, -4f),
                new Vector3(6f, 0f, 4f), new Vector3(-6f, 0f, 4f) };
            if (reverse) Array.Reverse(polygon);
            var slot = EnvironmentPlacementPresenter.WallFixtureSlot(new Vector3(5f, 2f, 0f), default, polygon, new Vector3(1f, 1f, .6f));
            var normal = EnvironmentPlacementPresenter.RotateYaw(Vector3.forward, slot.Yaw);
            Assert.That(slot.Position.x, Is.EqualTo(5.7f).Within(.00001f));
            Assert.That(normal.x, Is.EqualTo(-1f).Within(.00001f));
            Assert.That((slot.Position - normal * .3f).x, Is.EqualTo(6f).Within(.00001f));
        }

        [TestCase(false)] [TestCase(true)]
        public void TemplateFurnitureSkipPreservesOnlyExactLightSockets(bool empty)
        {
            var sockets = empty ? Array.Empty<Vector3>() : new[] { new Vector3(2f, 3f, 4f), new Vector3(-3f, 2f, -1f) };
            var slots = EnvironmentPresenter.BuildDressing(7, new Bounds(Vector3.up * 3.5f, new Vector3(12f, 7f, 12f)),
                false, true, null, lightSockets: sockets, authoredFurniture: true);
            Assert.That(slots.All(s => s.Torch), Is.True);
            Assert.That(slots.Select(s => s.Position), Is.EqualTo(sockets));
        }

        [Test]
        public void TemplateWithoutSocketProviderStillHasNoSupplementalFurniture()
        {
            var bounds = new Bounds(Vector3.up * 3.5f, new Vector3(12f, 7f, 12f));
            var slots = EnvironmentPresenter.BuildDressing(7, bounds, false, false, null, authoredFurniture: true);
            Assert.That(slots, Is.Not.Empty); Assert.That(slots.All(s => s.Torch), Is.True);
            Assert.That(EnvironmentPresenter.BuildDressing(7, bounds, false, false, null).Any(s => !s.Torch), Is.True);
        }

        [Test]
        public void WallContactIsAdmittedButProtrusionBeyondThePolygonIsRejected()
        {
            var boundary = new[] { new Vector3(-6f, 0f, -6f), new Vector3(6f, 0f, -6f),
                new Vector3(6f, 0f, 6f), new Vector3(-6f, 0f, 6f) };
            var bounds = new Bounds(Vector3.up * 3.5f, new Vector3(12f, 7f, 12f));
            var slots = EnvironmentPresenter.BuildDressing(7, bounds, false, false, null, boundary: boundary);
            Assert.That(slots.Any(s => s.Kind == EnvironmentDecorationKind.FloorProp), Is.True);
            Assert.That(EnvironmentPresenter.FitsBoundary(new EnvironmentSlot(new Vector3(0f, 1f, -5.6f), 0f,
                EnvironmentDecorationKind.FloorProp, new Vector3(2f, 2f, 1.1f)), boundary), Is.False);
        }
    }
}
