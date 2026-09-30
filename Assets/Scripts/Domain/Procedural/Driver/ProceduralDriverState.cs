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
//   - Index owned world-object sub-drivers by immutable interactable identity.
//   - Retain opt-in links, navigation sources and Passage sequences for teardown/rebakes.
//   - Retain puzzle modules, actor identity and this floor's optional kit bindings.
// DEPENDENCIES:
//   - Passive UnityEngine and navigation references only.
// USAGE NOTES:
//   No engine operations; ProceduralDriver creates and releases these objects.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralDriverState
    {
        public GameObject Root;
        public ProceduralRoomCatalogueData Catalogue;
        public string ThemeId;
        public EntityId PuzzlePlayer;
        public readonly List<ProceduralPuzzleModule> Puzzles = new List<ProceduralPuzzleModule>();
        public readonly List<Material> OwnedMaterials = new List<Material>();
        public readonly List<Mesh> OwnedMeshes = new List<Mesh>();
        public NavMeshData NavigationData;
        public NavMeshDataInstance NavigationInstance;
        public readonly List<NavMeshLinkInstance> NavigationLinks = new List<NavMeshLinkInstance>();
        public readonly List<NavMeshBuildSource> NavigationSources = new List<NavMeshBuildSource>();
        public NavMeshBuildSettings NavigationSettings;
        public Bounds NavigationBounds;
        public ProceduralDriverConfig Config;
        public Material PassageMaterial;
        public readonly List<ProceduralPassageDriverState> Passages = new List<ProceduralPassageDriverState>();
        public readonly List<LevelAnchor> LinedPocketAnchors = new List<LevelAnchor>();
        public int BlockCount;
        public bool Ready;
        public IReadOnlyList<LevelMarkerRecord> TraversalMarkers;
        public readonly Dictionary<int, List<GameObject>> Fragments = new Dictionary<int, List<GameObject>>();
        public readonly Dictionary<int, List<ProceduralBlock>> FragmentPlans = new Dictionary<int, List<ProceduralBlock>>();
        public readonly Dictionary<int, Bounds> RoomBounds = new Dictionary<int, Bounds>();
        public readonly Dictionary<int, Material> CrackMaterials = new Dictionary<int, Material>();
        public Texture2D CrackTexture;
        public readonly Dictionary<int, ProceduralWorldObject> Interactables = new Dictionary<int, ProceduralWorldObject>();
    }
}
