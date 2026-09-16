// ============================================================================
// HunterAttackDriverConfig.cs
// ============================================================================
// PURPOSE:
//   Supplies authored assets and tuning to the named Hunter engine concern.
//   Separate configurations let imported creature rigs and attack visuals change
//   without changing authoritative collision or accepted damage rules.
// ARCHITECTURAL ROLE:
//   DriverConfig (section 7d) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Keep per-life state separate from shared configuration and foreign systems.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Shared immutable config; deterministic editor setup assigns asset references.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter
{
    [CreateAssetMenu(menuName = "Worsen/Hunter/Attack Driver Config")]
    public sealed class HunterAttackDriverConfig : ScriptableObject
    {
        [SerializeField] private Material _warningMaterial;
        [SerializeField] private Material _projectileMaterial;
        [SerializeField] private Material _spikeMaterial;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private GameObject _spikePrefab;
        [SerializeField] private LayerMask _collisionMask = ~0;
        [SerializeField] private Color _warningColor = new Color(0.85f, 0.25f, 1f, 1f);
        public Material WarningMaterial => _warningMaterial;
        public Material ProjectileMaterial => _projectileMaterial;
        public Material SpikeMaterial => _spikeMaterial;
        public GameObject ProjectilePrefab => _projectilePrefab;
        public GameObject SpikePrefab => _spikePrefab;
        public int CollisionMask => _collisionMask.value;
        public Color WarningColor => _warningColor;
    }
}
