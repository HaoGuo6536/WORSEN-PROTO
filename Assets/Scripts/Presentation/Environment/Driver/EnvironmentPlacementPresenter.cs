// ============================================================================
// EnvironmentPlacementPresenter.cs
// ============================================================================
// PURPOSE:
//   Fits measured mesh footprints without relying on active renderer bounds.
//   Floor, wall and ceiling contact are solved before the Driver creates lighting,
//   so a displaced prefab pivot cannot make the art float or detach from its light.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Environment.
// KEY RESPONSIBILITIES:
//   - Reject implausible uniform fits and align long horizontal axes.
//   - Solve floor-bottom, wall-back and ceiling-top contact from measured bounds.
//   - Transform mesh bounds and locate the visible fixture's light source.
// DEPENDENCIES:
//   - Own config defaults and Unity value types only.
// USAGE NOTES:
//   Bounds are in the placement frame, before its world yaw. Positive Z faces
//   into the room. A slot remains an envelope centre; its back is the wall face.
//   No engine calls, randomness or clock; scale zero means reject, never spawn.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Worsen.Presentation.Environment
{
    public static class EnvironmentPlacementPresenter
    {
        public static float FitScale(Vector3 size, Vector3 envelope, float minimum, float maximum)
        {
            if (!Finite(size) || !Finite(envelope) || !Finite(minimum) || !Finite(maximum) ||
                minimum <= 0f || maximum < minimum || size.x <= 0f || size.y <= 0f || size.z <= 0f ||
                envelope.x <= 0f || envelope.y <= 0f || envelope.z <= 0f) return 0f;
            float fit = Math.Min(maximum, Math.Min(envelope.x / size.x, Math.Min(envelope.y / size.y, envelope.z / size.z)));
            return fit < minimum ? 0f : fit;
        }

        public static float LongAxisYaw(Vector3 size) => size.z > size.x ? 90f : 0f;

        public static Vector3 RotateYaw(Vector3 point, float yaw)
        {
            double radians = yaw * Math.PI / 180d;
            float sin = (float)Math.Sin(radians), cos = (float)Math.Cos(radians);
            return new Vector3(cos * point.x + sin * point.z, point.y, -sin * point.x + cos * point.z);
        }

        public static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(bounds.min), Vector3.zero);
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
                result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(x == 0 ? bounds.min.x : bounds.max.x,
                    y == 0 ? bounds.min.y : bounds.max.y, z == 0 ? bounds.min.z : bounds.max.z)));
            return result;
        }

        public static Bounds RotateBounds(Bounds bounds, float yaw)
        {
            var center = RotateYaw(bounds.center, yaw);
            var right = RotateYaw(Vector3.right, yaw); var forward = RotateYaw(Vector3.forward, yaw);
            return new Bounds(center, new Vector3(Math.Abs(right.x) * bounds.size.x + Math.Abs(forward.x) * bounds.size.z,
                bounds.size.y, Math.Abs(right.z) * bounds.size.x + Math.Abs(forward.z) * bounds.size.z));
        }

        public static Vector3 PlacementOffset(Bounds mesh, Vector3 envelope, float scale, bool floor, bool ceiling)
        {
            return new Vector3(-mesh.center.x * scale,
                floor ? -envelope.y * .5f - mesh.min.y * scale :
                ceiling ? envelope.y * .5f - mesh.max.y * scale : -mesh.center.y * scale,
                ceiling ? -mesh.center.z * scale : -envelope.z * .5f - mesh.min.z * scale);
        }

        public static Vector3 FixtureSource(Bounds placed, bool ceiling)
            => ceiling ? new Vector3(placed.center.x, placed.min.y, placed.center.z) :
                new Vector3(placed.center.x, placed.center.y, placed.max.z);

        public static EnvironmentSlot CeilingFixtureSlot(Vector3 socket, float ceiling, Vector3 envelope)
            => new EnvironmentSlot(new Vector3(socket.x, ceiling - envelope.y * .5f, socket.z), 0f,
                EnvironmentDecorationKind.Torch, envelope);

        public static EnvironmentSlot WallFixtureSlot(Vector3 socket, Bounds room, IReadOnlyList<Vector3> boundary,
            Vector3 envelope, IReadOnlyList<Bounds> cells = null)
        {
            if (boundary == null || boundary.Count < 3)
            {
                if (cells != null)
                    foreach (var cell in cells)
                        if (socket.x >= cell.min.x && socket.x <= cell.max.x && socket.z >= cell.min.z && socket.z <= cell.max.z)
                        { room = cell; break; }
                boundary = new[] { new Vector3(room.min.x, 0f, room.min.z), new Vector3(room.max.x, 0f, room.min.z),
                    new Vector3(room.max.x, 0f, room.max.z), new Vector3(room.min.x, 0f, room.max.z) };
            }
            float area = 0f;
            for (int i = 0; i < boundary.Count; i++)
            { var a = boundary[i]; var b = boundary[(i + 1) % boundary.Count]; area += a.x * b.z - b.x * a.z; }
            float closest = float.PositiveInfinity; Vector3 back = socket, inward = Vector3.forward;
            for (int i = 0; i < boundary.Count; i++)
            {
                var a = boundary[i]; var edge = boundary[(i + 1) % boundary.Count] - a; edge.y = 0f;
                if (edge.sqrMagnitude <= .000001f) continue;
                var delta = socket - a; delta.y = 0f;
                float along = Math.Max(0f, Math.Min(1f, Vector3.Dot(delta, edge) / edge.sqrMagnitude));
                var point = a + edge * along; point.y = socket.y;
                float distance = (point - socket).sqrMagnitude;
                if (distance >= closest) continue;
                closest = distance; back = point;
                inward = new Vector3(-edge.z, 0f, edge.x) / (float)Math.Sqrt(edge.sqrMagnitude) * (area < 0f ? -1f : 1f);
            }
            float yaw = (float)(Math.Atan2(inward.x, inward.z) * 180d / Math.PI);
            return new EnvironmentSlot(back + inward * (envelope.z * .5f), yaw, EnvironmentDecorationKind.Torch, envelope);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
