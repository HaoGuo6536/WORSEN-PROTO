// ============================================================================
// ProceduralDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains the generated engine objects and navigation allocation until the
//   owning Driver tears down the floor. Explicit ownership prevents geometry,
//   runtime materials or navigation data leaking into later rounds.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Track generated root, owned materials, fragment bases and navigation data.
//   - Retain per-room fissure materials and one shared procedural crack texture.
// DEPENDENCIES:
//   - Passive UnityEngine and navigation references only.
// USAGE NOTES:
//   No engine operations; ProceduralDriver creates and releases these objects.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralDriverState
    {
        public GameObject Root;
        public readonly List<Material> OwnedMaterials = new List<Material>();
        public NavMeshData NavigationData;
        public NavMeshDataInstance NavigationInstance;
        public int BlockCount;
        public bool Ready;
        public IReadOnlyList<LevelMarkerRecord> TraversalMarkers;
        public readonly Dictionary<int, List<GameObject>> Fragments = new Dictionary<int, List<GameObject>>();
        public readonly Dictionary<int, List<ProceduralBlock>> FragmentPlans = new Dictionary<int, List<ProceduralBlock>>();
        public readonly Dictionary<int, Bounds> RoomBounds = new Dictionary<int, Bounds>();
        public readonly Dictionary<int, Material> CrackMaterials = new Dictionary<int, Material>();
        public Texture2D CrackTexture;
    }
}
