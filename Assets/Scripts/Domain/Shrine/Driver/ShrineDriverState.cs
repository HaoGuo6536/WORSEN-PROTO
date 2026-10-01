// ============================================================================
// ShrineDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains the objects and private materials created for one floor's shrines.
//   The Driver alone manipulates and releases these passive engine references.
//   Per-shrine palette references let use dim just that shrine without touching shared assets.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Domain · Shrine.
// KEY RESPONSIBILITIES:
//   - Track owned placeholder geometry and material lifetimes.
//   - Associate each shrine id with its private body/accent materials and accent color.
// DEPENDENCIES:
//   - System collections and passive Unity references only.
// USAGE NOTES:
//   Scene-owned through ShrineDriver; Clear releases every recorded resource.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
namespace Worsen.Domain.Shrine
{
    public sealed class ShrineDriverState
    {
        public readonly Dictionary<int, GameObject> Objects = new Dictionary<int, GameObject>();
        public readonly List<Material> Materials = new List<Material>();
        public readonly Dictionary<int, Material> Bodies = new Dictionary<int, Material>();
        public readonly Dictionary<int, Material> Accents = new Dictionary<int, Material>();
        public readonly Dictionary<int, Color> AccentColors = new Dictionary<int, Color>();
    }
}
