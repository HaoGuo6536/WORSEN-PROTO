// ============================================================================
// EnvironmentDefinitions.cs
// ============================================================================
// PURPOSE:
//   Describes purely cosmetic wall placements before any objects are created.
//   Keeping placement data separate makes portal clearance independently testable.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Presentation · Environment.
// KEY RESPONSIBILITIES:
//   - Carry positions, orientation and the selected decoration role.
// DEPENDENCIES:
//   - Unity value types only.
// USAGE NOTES:
//   Internal presentation data. Callers pass room bounds and portal centers as primitives.
// ============================================================================
using UnityEngine;

namespace Worsen.Presentation.Environment
{
    public enum EnvironmentDecorationKind { Torch, Banner, Arch, Column, FloorProp, MerchantDisplay }

    public readonly struct EnvironmentSlot
    {
        public EnvironmentSlot(Vector3 position, float yaw, bool torch)
            : this(position, yaw, torch ? EnvironmentDecorationKind.Torch : EnvironmentDecorationKind.Banner,
                torch ? new Vector3(.8f, 1.1f, .6f) : new Vector3(1.3f, 1.4f, .35f)) { }
        public EnvironmentSlot(Vector3 position, float yaw, EnvironmentDecorationKind kind, Vector3 envelope)
        { Position = position; Yaw = yaw; Kind = kind; Envelope = envelope; }
        public Vector3 Position { get; }
        public float Yaw { get; }
        public EnvironmentDecorationKind Kind { get; }
        public Vector3 Envelope { get; }
        public bool Torch => Kind == EnvironmentDecorationKind.Torch;
    }
}
