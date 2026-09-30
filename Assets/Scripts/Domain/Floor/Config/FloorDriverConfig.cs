// ============================================================================
// FloorDriverConfig.cs
// ============================================================================
// PURPOSE:
//   Stores pickup, exit, warning and room-local collapse visual dimensions separately from game rules.
//   It also sets how far guidance looks past nearby path corners, independently
//   of the navigation sample radius and the gameplay refresh cadence.
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Tune the provisional gloved-hand scale independently of all collapse timings and reach.
//   - Tune code-built named cakes, candle flicker, layered glow and the placeholder trap tick.
//   - Reference native Lumen room and exit prefabs; no real-light fallback.
//   - Reference cake art and an optional medieval panel visual for the physical exit.
//   - Support staged cracks, tearing, mist advance and escapable hand contacts.
//   - Tune mist opacity modulation and crack-width gain for published warning pulses.
//   - Expose the horizontal guidance corner skip distance for designer tuning.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - UnityEngine serialized values and prefab/material references only.
//   - Consumed within Floor; no other system dependency.
// USAGE NOTES:
//   Consumed by FloorDriver and its owned sub-drivers; no global engine side effects.
//   Physical exit is opt-in so authored legacy floor fixtures retain their trigger behavior.
//   Guidance skip distance is provisional and measured horizontally in metres.
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
        [SerializeField] private string _pipedName = "ADA";
        [SerializeField, Min(0.01f)] private float _cakeVisualScale = 1f;
        [SerializeField] private Color _frostingColor = new Color(1f, 0.08f, 0.32f);
        [SerializeField] private Color _candleColor = new Color(1f, 0.48f, 0.08f);
        [SerializeField, Min(0f)] private float _candleIntensity = 1.5f;
        [SerializeField, Min(0.1f)] private float _candleRange = 2.5f;
        [SerializeField, Range(0f, 1f)] private float _candleFlickerDepth = 0.2f;
        [SerializeField, Min(0f)] private float _candleFlickerRate = 7f;
        [SerializeField, Min(0f)] private float _cakeGlowBrightness = 0.7f;
        [SerializeField, Min(0.1f)] private float _cakeGlowRadius = 0.6f;
        [SerializeField, Min(0.1f)] private float _cakePoolRadius = 1.2f;
        [SerializeField, Min(0.01f)] private float _trapTickDuration = 0.06f;
        [SerializeField, Min(1f)] private float _trapTickFrequency = 900f;
        public string PipedName => _pipedName;
        public float CakeVisualScale => Mathf.Max(0.01f, _cakeVisualScale);
        public Color FrostingColor => _frostingColor;
        public Color CandleColor => _candleColor;
        public float CandleIntensity => Mathf.Max(0f, _candleIntensity);
        public float CandleRange => Mathf.Max(0.1f, _candleRange);
        public float CandleFlickerDepth => Mathf.Clamp01(_candleFlickerDepth);
        public float CandleFlickerRate => Mathf.Max(0f, _candleFlickerRate);
        public float CakeGlowBrightness => Mathf.Max(0f, _cakeGlowBrightness);
        public float CakeGlowRadius => Mathf.Max(0.1f, _cakeGlowRadius);
        public float CakePoolRadius => Mathf.Max(0.1f, _cakePoolRadius);
        public float TrapTickDuration => Mathf.Max(0.01f, _trapTickDuration);
        public float TrapTickFrequency => Mathf.Max(1f, _trapTickFrequency);
        [SerializeField] private GameObject _lumenRoomWarningPrefab;
        [SerializeField] private GameObject _lumenExitGlowPrefab;
        [SerializeField] private float _pickupHeight = 0.7f;
        [SerializeField] private Vector3 _exitSize = new Vector3(2f, 3f, 2f);
        [SerializeField] private float _pathSampleRadius = 2f;
        [SerializeField, Min(0f)] private float _directionCornerSkipDistance = 1f;
        [SerializeField] private float _blockerThickness = 0.3f;
        [SerializeField] private float _warningPulsePeriod = 0.6f;
        [SerializeField] private float _warningIntensity = 5f;
        [SerializeField, Range(0f, 1f)] private float _warningMistPulseFloor = 0.5f;
        [SerializeField, Min(0f)] private float _warningCrackPulseGain = 1f;
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
        [SerializeField, Range(0.1f, 3f)] private float _glovedHandScaleMultiplier = 1.15f;
        public float GlovedHandScaleMultiplier => Mathf.Clamp(_glovedHandScaleMultiplier, 0.1f, 3f);
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
        public float DirectionCornerSkipDistance => Mathf.Max(0f, _directionCornerSkipDistance);
        public float BlockerThickness => _blockerThickness;
        public float WarningPulsePeriod => _warningPulsePeriod;
        public float WarningIntensity => _warningIntensity;
        public float WarningMistPulseFloor => Mathf.Clamp01(_warningMistPulseFloor);
        public float WarningCrackPulseGain => Mathf.Max(0f, _warningCrackPulseGain);
        public Color CakeColor => _cakeColor;
        public Color GoldenColor => _goldenColor;
        public Color WarningColor => _warningColor;
        public Color ClosedColor => _closedColor;
        public Color ExitLockedColor => _exitLockedColor;
        public Color ExitOpenColor => _exitOpenColor;
    }
}
