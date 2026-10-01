// ============================================================================
// MimicDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains renderer snapshots while one Mimic changes disguise or becomes spent.
//   Shared materials and the prefab remain untouched; teardown restores each
//   renderer's original enabled flag and property block for a clean next life.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Domain · Hunter Mimic.
// KEY RESPONSIBILITIES:
//   - Store per-renderer original flags and property blocks plus the golden tint.
// DEPENDENCIES:
//   - UnityEngine passive references and values only.
// USAGE NOTES:
//   Owned exclusively by MimicDriver; contains no engine operations or events.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter.Archetypes.Mimic
{
    public sealed class MimicDriverState
    {
        internal Renderer[] Renderers;
        internal bool[] Enabled;
        internal MaterialPropertyBlock[] Original;
        internal MaterialPropertyBlock Tint;
        internal Color GoldenColor;
    }
}
