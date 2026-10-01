// ============================================================================
// PlayerArmsGeometryTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the runtime Presenter against generated arm vertices without Unity.
//   The Blender validator independently matches this fixture to the exported FBX,
//   so view-composition assertions use real geometry rather than guessed bounds.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Check eye-relative composition at the default vertical field of view.
//   - Check yaw-only, swung arm bounds against steep-view near planes.
// DEPENDENCIES:
//   - PlayerLimbPresenter, PlayerMoverDriverConfig defaults, NUnit and value math.
//   - Repository-local ArtSource vertex fixture; no engine or importer APIs.
// USAGE NOTES:
//   Headless and Edit Mode. Run the Blender validator to establish FBX parity.
//   Unity import handedness and real skinned bounds remain coordinator checks.
// ============================================================================
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Player;

namespace Worsen.Tests.Player
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class PlayerArmsGeometryTests
    {
        private static Vector3[] Vertices(bool left)
        {
            string text = File.ReadAllText("ArtSource/Player/BlockyCharacter/RelaxedArms.geometry.json");
            MatchCollection matches = Regex.Matches(text, "\"position\"\\s*:\\s*\\[([^]]+)\\]");
            Assert.That(matches.Count, Is.EqualTo(48));
            return matches.Cast<Match>().Skip(left ? 0 : 24).Take(24).Select(match =>
            {
                float[] values = match.Groups[1].Value.Split(',').Select(value => float.Parse(value, CultureInfo.InvariantCulture)).ToArray();
                return new Vector3(values[0], values[1], values[2]);
            }).ToArray();
        }

        private static void View(float pitch, float yawDegrees, float roll, out Vector3 forward, out Vector3 up, out Vector3 right)
        {
            double p = pitch * Math.PI / 180, y = yawDegrees * Math.PI / 180, r = roll * Math.PI / 180;
            forward = new Vector3((float)(Math.Sin(y) * Math.Cos(p)), (float)-Math.Sin(p), (float)(Math.Cos(y) * Math.Cos(p)));
            Vector3 unrolledUp = new Vector3((float)(Math.Sin(y) * Math.Sin(p)), (float)Math.Cos(p), (float)(Math.Cos(y) * Math.Sin(p)));
            Vector3 unrolledRight = new Vector3((float)Math.Cos(y), 0f, (float)-Math.Sin(y));
            up = unrolledUp * (float)Math.Cos(r) - unrolledRight * (float)Math.Sin(r);
            right = unrolledRight * (float)Math.Cos(r) + unrolledUp * (float)Math.Sin(r);
        }

        [TestCase(0f)] [TestCase(45f)] [TestCase(-30f)]
        public void DefaultLensKeepsRelaxedArmsOutOfForwardCentreAndUpwardView(float pitch)
        {
            var presenter = new PlayerLimbPresenter();
            View(pitch, 0f, 0f, out Vector3 forward, out Vector3 up, out Vector3 right);
            float tanY = (float)Math.Tan(75 * Math.PI / 360), tanX = tanY * 16f / 9f;
            foreach (bool left in new[] { true, false })
            foreach (float swing in new[] { -8f, 0f, 8f })
            {
                Quaternion yaw = presenter.YawRotation(forward, Vector3.forward);
                Quaternion rotation = presenter.ArmRotation(yaw, swing);
                Vector3[] local = Vertices(left).Select(p => rotation * p).ToArray();
                Vector3 shoulder = presenter.ShoulderOffset(PlayerMoverDriverConfig.DefaultShoulderOffset, left, yaw);
                Bounds bounds = BoundsOf(local);
                bool below = presenter.BelowView(shoulder + bounds.center, bounds.extents, forward, up, 75f);
                Vector3 anchor = presenter.ClearNearPlane(shoulder, forward, 0.05f,
                    Math.Max(0f, presenter.RearExtent(bounds.center, bounds.extents, forward)));
                int visible = 0;
                foreach (Vector3 vertex in local)
                {
                    Vector3 point = anchor + vertex;
                    Assert.That(point.y, Is.LessThan(0f), "The hands and arms remain below the eye.");
                    float z = Vector3.Dot(point, forward), x = Vector3.Dot(point, right), y = Vector3.Dot(point, up);
                    Assert.That(z, Is.GreaterThan(0.05f));
                    if (below) continue;
                    if (pitch == 0f)
                        // A separating plane rejects the entire polygon, including
                        // edges that might cross the screen with no vertex inside.
                        Assert.That(y + z * tanY, Is.LessThan(0f), "No upper arm or central-screen intrusion at any swing phase.");
                    if (Math.Abs(x) <= z * tanX && Math.Abs(y) <= z * tanY) visible++;
                }
                if (pitch == 45f) Assert.That(visible, Is.GreaterThan(0), "Each hanging arm must actually enter the downward view.");
                if (pitch == -30f) Assert.That(below, Is.True, "Clearance must not pull invisible arms into an upward view.");
            }
        }

        [TestCase(0.05f)] [TestCase(0.3f)]
        public void EverySwungVertexClearsTheNearPlaneAcrossPitchYawAndRoll(float near)
        {
            var presenter = new PlayerLimbPresenter();
            foreach (float pitch in new[] { -85f, -30f, 0f, 45f, 85f, 90f })
            foreach (float heading in new[] { 0f, 70f, 180f })
            foreach (float roll in new[] { -12f, 0f, 12f })
            foreach (bool left in new[] { true, false })
            foreach (float swing in new[] { -8f, 0f, 8f })
            {
                View(pitch, heading, roll, out Vector3 forward, out _, out _);
                View(0f, heading, 0f, out Vector3 fallback, out _, out _);
                Quaternion yaw = presenter.YawRotation(forward, fallback);
                Quaternion rotation = presenter.ArmRotation(yaw, swing);
                Vector3[] vertices = Vertices(left).Select(p => rotation * p).ToArray();
                Bounds bounds = BoundsOf(vertices);
                Vector3 shoulder = presenter.ShoulderOffset(PlayerMoverDriverConfig.DefaultShoulderOffset, left, yaw);
                Vector3 anchor = presenter.ClearNearPlane(shoulder, forward, near,
                    Math.Max(0f, presenter.RearExtent(bounds.center, bounds.extents, forward)));
                foreach (Vector3 p in vertices) Assert.That(Vector3.Dot(anchor + p, forward), Is.GreaterThan(near));
                Assert.That((yaw * Vector3.up - Vector3.up).sqrMagnitude, Is.LessThan(0.000001f));
            }
        }

        private static Bounds BoundsOf(Vector3[] vertices)
        {
            Vector3 min = vertices[0], max = vertices[0];
            foreach (Vector3 point in vertices) { min = Vector3.Min(min, point); max = Vector3.Max(max, point); }
            return new Bounds((min + max) * 0.5f, max - min);
        }
    }
}
