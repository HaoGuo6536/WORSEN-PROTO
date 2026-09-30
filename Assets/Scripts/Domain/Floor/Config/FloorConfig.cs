// ============================================================================
// FloorConfig.cs
// ============================================================================
// PURPOSE:
//   Stores collection weights and collapse timing as designer-owned tuning.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Scale collapse durations by a provisional 0.75 for Faster Collapse (not hand timers).
//   - Tune optional trap replacement, audible tells and the Greedy Door threshold.
//   - Tune per-room placement, the required share and shared pickup loudness.
//   - Tune the simulation-time delay before an explicitly activated pocket starts its warning.
//   - Tune outward hand throws, boundary springs, accelerating warnings and opt-in collapse speed.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   Mirrored asset: ScriptableObjects/Domain/Floor/FloorConfig. Runtime getters only.
//   Room density supersedes legacy required-count overrides unless explicitly disabled.
//   No persistent singleton or competing simulation tick is created.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Floor
{
    [CreateAssetMenu(menuName = "Worsen/Floor/Floor Config")]
    public sealed class FloorConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int _requiredCakeCount = 10;
        [SerializeField, Min(1)] private int _trapStartRound = 3;
        [SerializeField, Range(0f, 1f)] private float _optionalTrapShare = 0.33333334f;
        [SerializeField, Min(1)] private int _roomsPerTrap = 3;
        [SerializeField, Min(0)] private int _maximumTraps = 3;
        [SerializeField, Min(0)] private int _extraBlinderTraps = 2;
        [SerializeField, Min(0.01f)] private float _trapTickInterval = 2f;
        [SerializeField, Range(0f, 1f)] private float _trapTickLoudness = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _trapAnnounceLoudness = 1f;
        [SerializeField, Range(0f, 1f)] private float _greedyDoorShare = 0.4f;
        public int TrapStartRound => Mathf.Max(1, _trapStartRound);
        public float OptionalTrapShare => Mathf.Clamp01(_optionalTrapShare);
        public int RoomsPerTrap => Mathf.Max(1, _roomsPerTrap);
        public int MaximumTraps => Mathf.Max(0, _maximumTraps);
        public int ExtraBlinderTraps => Mathf.Max(0, _extraBlinderTraps);
        public float TrapTickInterval => _trapTickInterval > 0f ? _trapTickInterval : 2f;
        public float TrapTickLoudness => Mathf.Clamp01(_trapTickLoudness);
        public float TrapAnnounceLoudness => Mathf.Clamp01(_trapAnnounceLoudness);
        public float GreedyDoorShare => Mathf.Clamp01(_greedyDoorShare);
        [SerializeField] private bool _useRoomCakeDensity = true;
        [SerializeField, Min(1)] private int _minimumCakesPerRoom = 1;
        [SerializeField, Min(1)] private int _maximumCakesPerRoom = 3;
        [SerializeField, Min(0)] private int _minimumExitRoomCakes = 0;
        [SerializeField, Range(0f, 1f)] private float _requiredCakeFraction = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _pickupNoiseLoudness = 0.6f;

        [SerializeField, Min(0f)] private float _flowWeight = 5f;
        [SerializeField, Min(0f)] private float _precisionWeight = 3f;
        [SerializeField, Min(0f)] private float _detourWeight = 2f;
        [SerializeField, Min(0f)] private float _riskWeight = 1f;
        [SerializeField, Min(0f)] private float _verticalWeight = 2f;
        [SerializeField, Min(0.01f)] private float _collapseInterval = 12f;
        [SerializeField, Min(0.01f)] private float _pocketCollapseDelay = 6f;
        public float PocketCollapseDelay => _pocketCollapseDelay > 0f && !float.IsInfinity(_pocketCollapseDelay) ? _pocketCollapseDelay : 6f;
        [SerializeField, Min(0.01f)] private float _telegraphDuration = 6f;
        [SerializeField, Min(0.01f)] private float _directionCueInterval = 0.5f;
        [SerializeField, Min(0.1f)] private float _tearingDuration = 2f;
        [SerializeField, Min(0.1f)] private float _encroachingDuration = 6f;
        [SerializeField, Min(0.1f)] private float _handWarningDuration = 0.7f;
        [SerializeField, Min(0.1f)] private float _handEscapeGrace = 1.4f;
        [SerializeField, Min(0.1f)] private float _handCooldown = 2f;
        [SerializeField, Range(0.15f, 0.9f)] private float _handSlowMultiplier = 0.5f;
        [SerializeField, Min(1f)] private float _handDamage = 25f;
        [SerializeField, Min(0.2f)] private float _handReach = 1.7f;
        [SerializeField, Min(0.3f)] private float _handEscapeDistance = 2.1f;
        [SerializeField, Min(0.1f)] private float _handThrowSpeed = 8f;
        [SerializeField, Min(0f)] private float _boundaryContactAcceleration = 4f;
        [SerializeField, Min(0.1f)] private float _boundarySpringAcceleration = 18f;
        [SerializeField, Range(0f, 1f)] private float _handNoiseLoudness = 0.8f;
        [SerializeField, Min(0.1f)] private float _warningPulseStartRate = 0.5f;
        [SerializeField, Min(0.1f)] private float _warningPulseEndRate = 4f;
        [SerializeField, Range(0.01f, 1f)] private float _fasterCollapseDurationMultiplier = 0.75f;
        public float HandThrowSpeed => _handThrowSpeed > 0f ? _handThrowSpeed : 8f;
        public float BoundaryContactAcceleration => Mathf.Max(0f, _boundaryContactAcceleration);
        public float BoundarySpringAcceleration => _boundarySpringAcceleration > 0f ? _boundarySpringAcceleration : 18f;
        public float HandNoiseLoudness => Mathf.Clamp01(_handNoiseLoudness);
        public float WarningPulseStartRate => _warningPulseStartRate > 0f ? _warningPulseStartRate : 0.5f;
        public float WarningPulseEndRate => Mathf.Max(WarningPulseStartRate, _warningPulseEndRate > 0f ? _warningPulseEndRate : 4f);
        public float FasterCollapseDurationMultiplier => _fasterCollapseDurationMultiplier > 0f &&
            _fasterCollapseDurationMultiplier <= 1f ? _fasterCollapseDurationMultiplier : 0.75f;
        public float FasterCollapseMultiplier => 1f / FasterCollapseDurationMultiplier;
        public float TearingDuration => _tearingDuration > 0f ? _tearingDuration : 2f;
        public float EncroachingDuration => _encroachingDuration > 0f ? _encroachingDuration : 6f;
        public float HandWarningDuration => _handWarningDuration > 0f ? _handWarningDuration : 0.7f;
        public float HandEscapeGrace => _handEscapeGrace > 0f ? _handEscapeGrace : 1.4f;
        public float HandCooldown => _handCooldown > 0f ? _handCooldown : 2f;
        public float HandSlowMultiplier => _handSlowMultiplier > 0f ? Mathf.Clamp(_handSlowMultiplier, 0.15f, 0.9f) : 0.5f;
        public float HandDamage => _handDamage > 0f ? _handDamage : 25f;
        public float HandReach => _handReach > 0f ? _handReach : 1.7f;
        public float HandEscapeDistance => Mathf.Max(HandReach + 0.2f, _handEscapeDistance > 0f ? _handEscapeDistance : 2.1f);
        public int RequiredCakeCount => _requiredCakeCount;
        public bool UseRoomCakeDensity => _useRoomCakeDensity;
        public int MinimumCakesPerRoom => _minimumCakesPerRoom;
        public int MaximumCakesPerRoom => _maximumCakesPerRoom;
        public int MinimumExitRoomCakes => _minimumExitRoomCakes;
        public float RequiredCakeFraction => _requiredCakeFraction;
        public float PickupNoiseLoudness => _pickupNoiseLoudness;

        public float FlowWeight => _flowWeight;
        public float PrecisionWeight => _precisionWeight;
        public float DetourWeight => _detourWeight;
        public float RiskWeight => _riskWeight;
        public float VerticalWeight => _verticalWeight;
        public float CollapseInterval => _collapseInterval;
        public float TelegraphDuration => _telegraphDuration;
        public float DirectionCueInterval => _directionCueInterval;
    }
}