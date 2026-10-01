// ============================================================================
// StareDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains the placeholder's original renderer and collider enabled flags.
//   Logical disappearance can then be reversed without enabling authored-disabled
//   components or disabling the HunterManager that owns the return clock.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Domain · Hunter archetype presentation.
// KEY RESPONSIBILITIES:
//   - Hold per-instance engine references as passive data only.
//   - Retain the Driver-rented capsule overlap buffer.
// DEPENDENCIES:
//   - Unity renderer and collider reference types.
// USAGE NOTES:
//   Owned by StareDriver; restored on teardown and never shared across instances.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter.Archetypes.Stare
{
    public sealed class StareDriverState
    {
        internal Renderer[] Renderers;
        internal Collider[] Colliders;
        internal Collider[] QueryOverlaps;
        internal bool[] RenderEnabled, ColliderEnabled;
    }
}
