// ============================================================================
// HeldItemPart.cs
// ============================================================================
// PURPOSE:
//   Describes an authored placeholder silhouette as primitive parts and transforms.
//   Keeping the data separate from engine construction makes the same geometry
//   available to pure tests and headless visual inspection.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Presentation · HeldItem.
// KEY RESPONSIBILITIES:
//   - Carry immutable primitive shape, local position, dimensions and palette index.
// DEPENDENCIES:
//   Unity value types only; no engine objects.
// USAGE NOTES:
//   System-local data, never passed to gameplay. Dimensions use Unity primitive units.
// ============================================================================
using UnityEngine;
namespace Worsen.Presentation.HeldItem
{
    public readonly struct HeldItemPart
    {
        public HeldItemPart(PrimitiveType shape, Vector3 position, Vector3 scale, bool detail = false)
        { Shape = shape; Position = position; Scale = scale; Detail = detail; }
        public PrimitiveType Shape { get; }
        public Vector3 Position { get; }
        public Vector3 Scale { get; }
        public bool Detail { get; }
    }
}
