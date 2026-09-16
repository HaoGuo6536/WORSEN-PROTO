// ============================================================================
// FloorDriverConfig.cs
// ============================================================================
// PURPOSE:
//   Stores pickup, exit, warning and physical blocker dimensions separately from game rules.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Reference a project-owned cake slice prefab while preserving trigger dimensions.
//   - Implement the Floor responsibility named by this file.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   Consumed by FloorDriver and its owned sub-drivers; no global engine side effects.
//   No persistent singleton or competing simulation tick is created.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Floor
{
    [CreateAssetMenu(menuName = "Worsen/Floor/Driver Config")]
    public sealed class FloorDriverConfig : ScriptableObject
    {
        [SerializeField] private float _pickupRadius = 0.55f;
        [SerializeField] private GameObject _cakePrefab;
        [SerializeField] private float _pickupHeight = 0.7f;
        [SerializeField] private Vector3 _exitSize = new Vector3(2f, 3f, 2f);
        [SerializeField] private float _pathSampleRadius = 2f;
        [SerializeField] private float _blockerThickness = 0.3f;
        [SerializeField] private float _warningPulsePeriod = 0.6f;
        [SerializeField] private float _warningIntensity = 5f;
        [SerializeField] private Color _cakeColor = new Color(1f, 0.85f, 0.7f);
        [SerializeField] private Color _goldenColor = new Color(1f, 0.65f, 0.02f);
        [SerializeField] private Color _warningColor = new Color(1f, 0.08f, 0.02f);
        [SerializeField] private Color _closedColor = new Color(0.18f, 0.005f, 0.005f);
        [SerializeField] private Color _exitLockedColor = new Color(0.5f, 0.05f, 0.03f);
        [SerializeField] private Color _exitOpenColor = new Color(0.1f, 1f, 0.3f);
        public float PickupRadius => _pickupRadius;
        public GameObject CakePrefab => _cakePrefab;
        public float PickupHeight => _pickupHeight;
        public Vector3 ExitSize => _exitSize;
        public float PathSampleRadius => _pathSampleRadius;
        public float BlockerThickness => _blockerThickness;
        public float WarningPulsePeriod => _warningPulsePeriod;
        public float WarningIntensity => _warningIntensity;
        public Color CakeColor => _cakeColor;
        public Color GoldenColor => _goldenColor;
        public Color WarningColor => _warningColor;
        public Color ClosedColor => _closedColor;
        public Color ExitLockedColor => _exitLockedColor;
        public Color ExitOpenColor => _exitOpenColor;
    }
}
