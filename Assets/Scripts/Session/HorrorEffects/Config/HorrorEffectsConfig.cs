// ============================================================================
// HorrorEffectsConfig.cs
// ============================================================================
// PURPOSE:
//   Provides designer tuning for light authority and spatial curse timing.
//   These values control gameplay facts without storing any live run state.
// ARCHITECTURAL ROLE:
//   Config (§4) · Session · HorrorEffects.
// KEY RESPONSIBILITIES:
//   Expose beam, delayed noise, optional-room and traversal perk tuning.
// DEPENDENCIES:
//   Core value contracts and the HorrorEffects system's own data only.
//   Unity value math is pure; engine lifecycle belongs only to the Manager.
// USAGE NOTES:
//   This immutable asset is explicitly supplied by scene setup; it owns no runtime state.
// ============================================================================
using UnityEngine;

namespace Worsen.Session.HorrorEffects
{
    [CreateAssetMenu(menuName = "Worsen/Horror Effects/Config")]
    public sealed class HorrorEffectsConfig : ScriptableObject
    {
        [SerializeField, Min(0.1f)] private float flashlightRange = 18f;
        [SerializeField, Range(1f, 179f)] private float flashlightCone = 52f;
        [SerializeField, Range(0.1f, 1f)] private float shutteredConeMultiplier = 0.58f;
        [SerializeField, Min(0.1f)] private float afterimageLifetime = 1.8f;
        [SerializeField, Min(0.1f)] private float echoDelay = 1.2f;
        [SerializeField, Min(0f)] private float echoLoudness = 1.2f;
        [SerializeField, Min(0f)] private float hardLandingStumbleThreshold = 0.3f;
        [SerializeField, Min(0.1f)] private float borrowedStepDelay = 2.1f;
        [SerializeField, Min(0.1f)] private float borrowedStepInterval = 0.65f;
        [SerializeField, Min(0.1f)] private float borrowedStepDistance = 1.2f;
        [SerializeField, Min(0f)] private float borrowedStepLoudness = 0.7f;
        [SerializeField, Min(0f)] private float gildedLoudness = 1.6f;
        [SerializeField, Min(0.1f)] private float optionalRoomInterval = 18f;
        [SerializeField, Min(0.1f)] private float flameRadius = 7f;
        [SerializeField, Range(0.1f, 1f)] private float flameDimMultiplier = 0.4f;
        [SerializeField, Min(0.1f)] private float flamePositionInterval = 0.25f;
        [SerializeField, Range(0f, 1f)] private float feltSolesMultiplier = 0.4f;
        [SerializeField, Range(0.1f, 1f)] private float climberRecoveryMultiplier = 0.75f;
        [SerializeField, Range(0f, 1f)] private float sealedWindowMultiplier = 0.4f;
        [SerializeField, Min(1)] private int maximumPendingNoises = 24;
        [SerializeField, Min(1)] private int maximumOptionalRooms = 128;
        [SerializeField, Min(1)] private int maximumChalkMarks = 256;
        public float FlashlightRange => flashlightRange;
        public float FlashlightCone => flashlightCone;
        public float ShutteredConeMultiplier => shutteredConeMultiplier;
        public float AfterimageLifetime => afterimageLifetime;
        public float EchoDelay => echoDelay;
        public float EchoLoudness => echoLoudness;
        public float HardLandingStumbleThreshold => hardLandingStumbleThreshold;
        public float BorrowedStepDelay => borrowedStepDelay;
        public float BorrowedStepInterval => borrowedStepInterval;
        public float BorrowedStepDistance => borrowedStepDistance;
        public float BorrowedStepLoudness => borrowedStepLoudness;
        public float GildedLoudness => gildedLoudness;
        public float OptionalRoomInterval => optionalRoomInterval;
        public float FlameRadius => flameRadius;
        public float FlameDimMultiplier => flameDimMultiplier;
        public float FlamePositionInterval => flamePositionInterval;
        public float FeltSolesMultiplier => feltSolesMultiplier;
        public float ClimberRecoveryMultiplier => climberRecoveryMultiplier;
        public float SealedWindowMultiplier => sealedWindowMultiplier;
        public int MaximumPendingNoises => maximumPendingNoises;
        public int MaximumOptionalRooms => maximumOptionalRooms;
        public int MaximumChalkMarks => maximumChalkMarks;
    }
}
