// ============================================================================
// ShrineDriverPresenter.cs
// ============================================================================
// PURPOSE:
//   Converts normalized shrine recipes into local positions and primitive scales.
//   It also computes the spent accent without compounding repeated use commands.
//   These calculations need no scene, material, time source or random generator.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Shrine.
// KEY RESPONSIBILITIES:
//   - Scale recipe positions and full dimensions to the configured shrine size.
//   - Compute clamped, alpha-preserving accent dimming and surface emission.
// DEPENDENCIES:
//   - Own DriverConfig recipe values, System math and Unity value types only.
// USAGE NOTES:
//   Stateless; the Driver applies results and retains every engine reference.
//   Recipes support cubes, spheres and cylinders; no text or flat one-sided geometry.
// ============================================================================
using System;
using UnityEngine;
namespace Worsen.Domain.Shrine
{
    public static class ShrineDriverPresenter
    {
        public static Vector3 Position(ShrineDriverConfig.Part part, Vector3 size) => Multiply(part.Position, size);
        public static Vector3 Scale(ShrineDriverConfig.Part part, Vector3 size)
        {
            var scale = Multiply(part.Dimensions, size);
            switch (part.Primitive)
            {
                case PrimitiveType.Cube:
                case PrimitiveType.Sphere: return scale;
                case PrimitiveType.Cylinder: return new Vector3(scale.x, scale.y * .5f, scale.z);
                default: throw new ArgumentOutOfRangeException(nameof(part), "Shrine recipes support Cube, Sphere and Cylinder.");
            }
        }
        public static Color DimAccent(Color color, float multiplier)
        {
            var value = Math.Max(0f, Math.Min(1f, multiplier));
            return new Color(color.r * value, color.g * value, color.b * value, color.a);
        }
        public static Color Emission(Color color, float strength, float multiplier)
        {
            var dimmed = DimAccent(color, multiplier);
            var value = Math.Max(0f, strength);
            return new Color(dimmed.r * value, dimmed.g * value, dimmed.b * value, 1f);
        }
        private static Vector3 Multiply(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
    }
}
