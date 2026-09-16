// ============================================================================
// ChaseConfig.cs
// ============================================================================
// PURPOSE:
//   Defines the confirmation, loss and proximity rules for one pursuit service. All runtime source timers live in state rather than this shared asset.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Chase.
// KEY RESPONSIBILITIES:
//   - Describe owned state and expose only read access across system boundaries.
// DEPENDENCIES:
//   - Core shared facts and the owning Chase system only.
// USAGE NOTES:
//   Scene-owned state; no event publication, engine calls, or independent simulation loop.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Chase
{
    [CreateAssetMenu(menuName = "Worsen/Chase/Chase Config")]
    public sealed class ChaseConfig : ScriptableObject
    {
        [SerializeField] private float _confirmationSeconds = 0.3f;
        [SerializeField] private float _lossSeconds = 2.5f;
        [SerializeField] private float _lossDistance = 14f;
        [SerializeField] private float _lostGraceSeconds = 1.5f;
        [SerializeField] private float _nearDistance = 4f;
        [SerializeField] private float _farDistance = 20f;
        [SerializeField] private float _rearWeight = 1f;
        [SerializeField] private float _frontWeight = 0.5f;
        public float ConfirmationSeconds => _confirmationSeconds;
        public float LossSeconds => _lossSeconds;
        public float LossDistance => _lossDistance;
        public float LostGraceSeconds => _lostGraceSeconds;
        public float NearDistance => _nearDistance;
        public float FarDistance => _farDistance;
        public float RearWeight => _rearWeight;
        public float FrontWeight => _frontWeight;
    }
}

