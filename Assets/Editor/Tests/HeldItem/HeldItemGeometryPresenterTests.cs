// ============================================================================
// HeldItemGeometryPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Checks that every supported manual consumable has a distinct bounded silhouette.
//   Pure part data is shared by runtime construction and the headless visual render,
//   keeping placeholder inspection independent of Unity asset imports.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · HeldItem.
// KEY RESPONSIBILITIES:
//   - Verify supported coverage, positive dimensions, distinct geometry and empty input.
// DEPENDENCIES:
//   NUnit and HeldItem pure presentation only.
// USAGE NOTES:
//   No primitive objects are created by this fixture.
// ============================================================================
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Presentation.HeldItem;
namespace Worsen.Tests.HeldItem
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HeldItemGeometryPresenterTests
    {
        [TestCase(40f, 16f / 9f)] [TestCase(60f, 16f / 9f)] [TestCase(80f, 21f / 9f)]
        public void EverySilhouetteFitsAtRestAndClearsTheBottomBeforeSwitching(float fov, float aspect)
        {
            var pose = new HeldItemPresenter(); var geometry = new HeldItemGeometryPresenter();
            float scale = .1f * pose.ProjectionScale(fov);

            var raised = pose.ViewPosition(new Vector3(.22f, -.12f, .45f), fov, aspect);
            var lowered = pose.ViewPosition(new Vector3(.22f, -.47f, .45f), fov, aspect);
            foreach (var id in new[] { "firecracker", "gauze", "smelling-salts", "wax-ward", "doorstop", "oil-flask", "glass-vial", "adrenaline" })
                foreach (var part in geometry.Build(id))
                    for (int x = -1; x <= 1; x += 2)
                        for (int y = -1; y <= 1; y += 2)
                            for (int z = -1; z <= 1; z += 2)
                            {
                                var local = part.Position + new Vector3(x * part.Scale.x * .5f,
                                    y * part.Scale.y * (part.Shape == PrimitiveType.Cylinder ? 1f : .5f), z * part.Scale.z * .5f);
                                var offset = RotateAuthoredPose(local) * scale;
                                var point = raised + offset;
                                float halfHeight = point.z * (float)System.Math.Tan(fov * System.Math.PI / 360d);
                                Assert.That(point.z, Is.GreaterThan(.1f), id);
                                Assert.That(point.x / (halfHeight * aspect), Is.InRange(0f, 1f), id);
                                Assert.That(point.y / halfHeight, Is.InRange(-1f, 0f), id);
                                Assert.That((lowered + offset).y / halfHeight, Is.LessThan(-1f), id + " must disappear below the view, not pop out");
                            }
        }
        // Pure equivalent of the authored Unity Euler pose (Z, then X, then Y).
        // Quaternion.Euler invokes native code and is deliberately not used here.
        private static Vector3 RotateAuthoredPose(Vector3 v)
        {
            double rx = -12d * System.Math.PI / 180d, ry = -18d * System.Math.PI / 180d, rz = 8d * System.Math.PI / 180d;
            double x = v.x * System.Math.Cos(rz) - v.y * System.Math.Sin(rz);
            double y = v.x * System.Math.Sin(rz) + v.y * System.Math.Cos(rz);
            double z = y * System.Math.Sin(rx) + v.z * System.Math.Cos(rx);
            y = y * System.Math.Cos(rx) - v.z * System.Math.Sin(rx);
            return new Vector3((float)(x * System.Math.Cos(ry) + z * System.Math.Sin(ry)), (float)y,
                (float)(-x * System.Math.Sin(ry) + z * System.Math.Cos(ry)));
        }
        [Test]
        public void AllManualItemsHaveDistinctBoundedComposedGeometry()
        {
            var shapes = new HashSet<string>(); var p = new HeldItemGeometryPresenter();
            foreach (var id in new[] { "firecracker", "gauze", "smelling-salts", "wax-ward", "doorstop", "oil-flask", "glass-vial", "adrenaline" })
            {
                Assert.That(new HeldItemPresenter().Supported(id), Is.True);
                var parts = p.Build(id); Assert.That(parts.Length, Is.GreaterThanOrEqualTo(2));
                string signature = "";
                foreach (var part in parts)
                {
                    Assert.That(part.Scale.x, Is.InRange(.001f, 2f)); Assert.That(part.Scale.y, Is.InRange(.001f, 2f)); Assert.That(part.Scale.z, Is.InRange(.001f, 2f));
                    Assert.That(part.Position.sqrMagnitude, Is.LessThan(4f));
                    signature += part.Shape + ":" + part.Position + ":" + part.Scale + ";";
                }
                Assert.That(shapes.Add(signature), Is.True, id);
            }
            Assert.That(p.Build(null), Is.Empty); Assert.That(p.Build(""), Is.Empty); Assert.That(p.Build("unknown"), Is.Empty);
        }
    }
}
