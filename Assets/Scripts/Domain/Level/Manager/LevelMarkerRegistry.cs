// ============================================================================
// LevelMarkerRegistry.cs
// ============================================================================
// PURPOSE:
//   Stores the active marker records reported through the owning Level Manager.
//   It provides enumeration by stable id without discovering scene objects or
//   making graph decisions.
// ARCHITECTURAL ROLE:
//   Registry (§8) · Domain · Level.
// KEY RESPONSIBILITIES:
//   - Register, unregister and enumerate immutable marker snapshots.
// DEPENDENCIES:
//   - Core level contracts; no other Domain system and no upper runtime layer.
// USAGE NOTES:
//   Scene-owned collection. Only LevelManager registers records; sub-drivers
//   report through LevelDriver and never access this Registry.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Level
{
    [DisallowMultipleComponent]
    public sealed class LevelMarkerRegistry : MonoBehaviour
    {
        private readonly SortedDictionary<int, LevelMarkerRecord> _records = new SortedDictionary<int, LevelMarkerRecord>();
        public IEnumerable<LevelMarkerRecord> Records => _records.Values;
        public int Count => _records.Count;
        public void Register(LevelMarkerRecord record) => _records.Add(record.Id, record);
        public bool Unregister(int id) => _records.Remove(id);
        public bool TryGet(int id, out LevelMarkerRecord record) => _records.TryGetValue(id, out record);
        public void Clear() => _records.Clear();
    }
}

