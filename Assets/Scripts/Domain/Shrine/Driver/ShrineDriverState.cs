// ============================================================================
// ShrineDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains the objects and private materials created for one floor's shrines.
//   The Driver alone manipulates and releases these passive engine references.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Domain · Shrine.
// KEY RESPONSIBILITIES:
//   - Track owned placeholder geometry and material lifetimes.
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
    }
}
