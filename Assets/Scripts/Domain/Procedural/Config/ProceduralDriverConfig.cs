// ============================================================================
// ProceduralDriverConfig.cs
// ============================================================================
// PURPOSE:
//   Stores the collision, material and navigation settings for generated rooms.
//   Floors remain physically enclosed by walls and ceilings, and the navigation
//   bake uses the same box descriptions that create the collision geometry.
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Configure enclosed shell thickness, materials and bounded navigation checks.
// DEPENDENCIES:
//   - UnityEngine materials and serialization; no other gameplay system.
// USAGE NOTES:
//   Assign imported or project materials through deterministic scene setup.
//   Missing materials use declared dark, rough runtime materials owned by Driver.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    [CreateAssetMenu(menuName = "Worsen/Procedural/Driver Config")]
    public sealed class ProceduralDriverConfig : ScriptableObject
    {
        [SerializeField] private float _wallThickness = 0.3f;
        [SerializeField] private float _floorThickness = 0.3f;
        [SerializeField] private float _ceilingThickness = 0.3f;
        [SerializeField] private Material _wallMaterial = null;
        [SerializeField] private Material _floorMaterial = null;
        [SerializeField] private Material _ceilingMaterial = null;
        [SerializeField] private Color _wallColor = new Color(0.19f, 0.21f, 0.2f);
        [SerializeField] private Color _floorColor = new Color(0.11f, 0.12f, 0.11f);
        [SerializeField] private Color _ceilingColor = new Color(0.09f, 0.1f, 0.09f);
        [SerializeField] private float _surfaceSmoothness = 0.08f;
        [SerializeField] private int _geometryLayer = 0;
        [SerializeField] private int _navMeshAgentTypeId = 0;
        [SerializeField] private float _navSampleRadius = 0.75f;
        [SerializeField] private float _navVoxelSize = 0.1f;
        [SerializeField] private float _navBoundsPadding = 1f;
        [SerializeField] private float _partitionLength = 7f;
        [SerializeField] private float _partitionThickness = 0.6f;
        [SerializeField] private float _shortcutWidth = 2.4f;
        [SerializeField] private float _vaultHeight = 1f;
        [SerializeField] private float _windowTopHeight = 3.25f;
        [SerializeField] private float _slideClearance = 1.05f;
        [SerializeField] private float _landingOffset = 1.05f;
        public float WallThickness => _wallThickness;
        public float FloorThickness => _floorThickness;
        public float CeilingThickness => _ceilingThickness;
        public Material WallMaterial => _wallMaterial;
        public Material FloorMaterial => _floorMaterial;
        public Material CeilingMaterial => _ceilingMaterial;
        public Color WallColor => _wallColor;
        public Color FloorColor => _floorColor;
        public Color CeilingColor => _ceilingColor;
        public float SurfaceSmoothness => _surfaceSmoothness;
        public int GeometryLayer => _geometryLayer;
        public int NavMeshAgentTypeId => _navMeshAgentTypeId;
        public float NavSampleRadius => _navSampleRadius;
        public float NavVoxelSize => _navVoxelSize;
        public float NavBoundsPadding => _navBoundsPadding;
        public float PartitionLength => _partitionLength;
        public float PartitionThickness => _partitionThickness;
        public float ShortcutWidth => _shortcutWidth;
        public float VaultHeight => _vaultHeight;
        public float WindowTopHeight => _windowTopHeight;
        public float SlideClearance => _slideClearance;
        public float LandingOffset => _landingOffset;
    }
}
