// ============================================================================
// HUDCompassPresenter.cs
// ============================================================================
// PURPOSE:
//   Projects a solid arrowhead and shaft onto a tilted compass plane.
//   Full camera-relative direction retains rear and vertical destinations.
// ARCHITECTURAL ROLE:
//   Presenter (section 7b) - Presentation - HUD.
// KEY RESPONSIBILITIES:
//   - Rotate a small three-dimensional needle and depth-sort its white shaded facets.
//   - Return bounded finite UI geometry without meshes, cameras or render targets.
// DEPENDENCIES:
//   System, Unity value types and HUDCompassFace; no engine calls or gameplay queries.
// USAGE NOTES:
//   Stateless calculation; the Driver provides current layout and camera-relative direction.
// ============================================================================
using System;
using UnityEngine;
namespace Worsen.Presentation.HUD
{
    public sealed class HUDCompassPresenter
    {
        public Vector2 Project(Rect bounds, Vector3 point)
        {
            float x = Finite(bounds.x) ? bounds.x : 0f, y = Finite(bounds.y) ? bounds.y : 0f;
            float width = Positive(bounds.width), height = Positive(bounds.height);
            float scale = Math.Min(width, height) * .38f;
            return new Vector2(x + width * .5f + point.x * scale,
                y + height * .5f - (point.y * .78f + point.z * .62f) * scale);
        }

        public Vector2[] BaseRing(Rect bounds)
        {
            var ring = new Vector2[32];
            for (int i = 0; i < ring.Length; i++)
            {
                double angle = i * Math.PI * 2.0 / ring.Length;
                ring[i] = Project(bounds, new Vector3((float)Math.Cos(angle), -.17f, (float)Math.Sin(angle)));
            }
            return ring;
        }

        public HUDCompassFace[] Needle(Rect bounds, Vector3 direction)
        {
            if (!Finite(direction.x) || !Finite(direction.y) || !Finite(direction.z)) return Array.Empty<HUDCompassFace>();
            double length = Math.Sqrt((double)direction.x * direction.x + (double)direction.y * direction.y + (double)direction.z * direction.z);
            if (length < .000001) return Array.Empty<HUDCompassFace>();
            Vector3 forward = new Vector3((float)(direction.x / length), (float)(direction.y / length), (float)(direction.z / length));
            double yaw = Math.Atan2(forward.x, forward.z);
            Vector3 right = new Vector3((float)Math.Cos(yaw), 0f, (float)-Math.Sin(yaw));
            Vector3 up = Vector3.Cross(forward, right);
            // A raised triangular arrowhead plus a rectangular solid shaft. Different
            // head/tail silhouettes retain directional meaning when the target is behind.
            var vertices = new[] {
                new Vector3(0,0,.9f), new Vector3(-.33f,0,-.02f), new Vector3(.33f,0,-.02f),
                new Vector3(0,.18f,.12f), new Vector3(0,-.12f,.12f),
                new Vector3(-.09f,-.07f,-.65f), new Vector3(.09f,-.07f,-.65f),
                new Vector3(.09f,.07f,-.65f), new Vector3(-.09f,.07f,-.65f),
                new Vector3(-.09f,-.07f,.12f), new Vector3(.09f,-.07f,.12f),
                new Vector3(.09f,.07f,.12f), new Vector3(-.09f,.07f,.12f)
            };
            int[] triangles = { 0,1,3, 0,3,2, 1,2,3, 0,4,1, 0,2,4, 1,4,2,
                5,6,7, 5,7,8, 9,12,11, 9,11,10, 5,9,10, 5,10,6,
                8,7,11, 8,11,12, 5,8,12, 5,12,9, 6,10,11, 6,11,7 };
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                vertices[i] = right * p.x + up * p.y + forward * p.z;
            }
            var faces = new HUDCompassFace[triangles.Length / 3];
            for (int i = 0; i < faces.Length; i++)
            {
                Vector3 a = vertices[triangles[i * 3]], b = vertices[triangles[i * 3 + 1]], c = vertices[triangles[i * 3 + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                float light = Mathf.Clamp01(Vector3.Dot(normal, new Vector3(-.36f, .8f, -.48f)));
                float white = .66f + .34f * light;
                faces[i] = new HUDCompassFace(Project(bounds, a), Project(bounds, b), Project(bounds, c),
                    new Color(white, white, white, 1f), Depth((a + b + c) / 3f));
            }
            Array.Sort(faces, (a, b) => a.Depth.CompareTo(b.Depth));
            return faces;
        }

        private static float Depth(Vector3 point) => point.y * .62f - point.z * .78f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Positive(float value) => Finite(value) ? Math.Max(0f, value) : 0f;
    }
}
