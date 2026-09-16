// ============================================================================
// FloorDriverConfig.cs
// ============================================================================
// PURPOSE:
//   Stores pickup, exit, warning and room-local collapse visual dimensions separately from game rules.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Reference native Lumen room and exit prefabs; no real-light fallback.
//   - Reference cake art and an optional medieval panel visual for the physical exit.
//   - Support staged cracks, tearing, mist advance and escapable hand contacts.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   Consumed by FloorDriver and its owned sub-drivers; no global engine side effects.
//   Physical exit is opt-in so authored legacy floor fixtures retain their trigger behavior.
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
        [SerializeField] private GameObject _lumenRoomWarningPrefab;
        [SerializeField] private GameObject _lumenExitGlowPrefab;
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
        [SerializeField] private GameObject _handPrefab;
        [SerializeField] private Material _crackMaterial;
        [SerializeField] private Material _mistMaterial;
        [SerializeField, Range(3, 6)] private int _handGridWidth = 5;
        [SerializeField, Range(0.05f, 0.8f)] private float _portalInset = 0.25f;
        [SerializeField, Range(0.1f, 3f)] private float _handVisualScale = 1f;
        [SerializeField] private bool _usePhysicalExitDoor;
        [SerializeField] private GameObject _exitDoorPrefab;
        [SerializeField] private Material _exitDoorMaterial;
        [SerializeField, Min(0.1f)] private float _exitDoorOpeningDuration = 1.2f;
        [SerializeField, Range(90f, 120f)] private float _exitDoorOpeningAngle = 100f;
        [SerializeField] private float _exitDoorPrefabYaw = 90f;
        [SerializeField] private float _exitDoorYaw;
        [SerializeField, Range(0.15f, 0.6f)] private float _exitCrossingDistance = 0.35f;
        public bool UsePhysicalExitDoor => _usePhysicalExitDoor;
        public GameObject ExitDoorPrefab => _exitDoorPrefab;
        public Material ExitDoorMaterial => _exitDoorMaterial;
        public float ExitDoorOpeningDuration => _exitDoorOpeningDuration > 0f ? _exitDoorOpeningDuration : 1.2f;
        public float ExitDoorOpeningAngle => Mathf.Clamp(_exitDoorOpeningAngle < 90f ? 100f : _exitDoorOpeningAngle, 90f, 120f);
        public float ExitDoorPrefabYaw => _exitDoorPrefabYaw;
        public float ExitDoorYaw => _exitDoorYaw;
        public float ExitCrossingDistance => Mathf.Clamp(_exitCrossingDistance > 0f ? _exitCrossingDistance : 0.35f, 0.15f, 0.6f);
        public GameObject HandPrefab => _handPrefab;
        public Material CrackMaterial => _crackMaterial;
        public Material MistMaterial => _mistMaterial;
        public int HandGridWidth => Mathf.Clamp(_handGridWidth == 0 ? 5 : _handGridWidth, 3, 6);
        public float PortalInset => Mathf.Max(0.05f, _portalInset);
        public float HandVisualScale => _handVisualScale > 0f ? _handVisualScale : 1f;
        public float PickupRadius => _pickupRadius;
        public GameObject CakePrefab => _cakePrefab;
        public GameObject LumenRoomWarningPrefab => _lumenRoomWarningPrefab;
        public GameObject LumenExitGlowPrefab => _lumenExitGlowPrefab;
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
