// ============================================================================
// PostFXDriverConfig.cs
// ============================================================================
//
// PURPOSE:
//   Stores the prototype's pursuit, injury and intrusion effect settings.
//   Each response can be tuned without changing the code that routes gameplay facts.
//
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Presentation · PostFX.
//
// KEY RESPONSIBILITIES:
//   - Supply Blind trap, Mirror Skin and brief Glimpse outline tuning.
//   - Configure red damage/low-health borders and exact catalogue ids for blindness.
//   - Expose optional re-acquire blur and bounded effect strength.
//   - Tune the text-free camcorder frame, constant degradation and timed intrusion/blindness.
//   - Keep runtime envelopes out of shared assets.
//
// DEPENDENCIES:
//   - No other project systems.
//
// USAGE NOTES:
//   - Mirrored Resources/ScriptableObjects/Presentation/PostFX asset; runtime read-only.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Presentation.PostFX
{
    [CreateAssetMenu(fileName = "PostFXDriverConfig", menuName = "Worsen/PostFX/Driver Config")]
    public sealed class PostFXDriverConfig : ScriptableObject
    {
        public const float DefaultMirrorSkinDurationMultiplier = 0.5f;
        [Header("Damage border (provisional)")]
        [SerializeField] private Color _damageVignetteColor = new Color(1f, 0.015f, 0.01f, 1f);
        [SerializeField, Range(0f, 1f)] private float _damageVignettePeak = 0.5f;
        [SerializeField, Range(0.01f, 1f)] private float _damageFullStrengthHealthFraction = 0.5f;
        [SerializeField, Min(0.001f)] private float _damageFadeSeconds = 1.25f;
        [SerializeField, Range(0.01f, 1f)] private float _damageVignetteSmoothness = 0.3f;
        [SerializeField, Range(0.01f, 1f)] private float _lowHealthFraction = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _lowHealthVignette = 0.25f;
        [SerializeField, Range(0f, 1f)] private float _lowHealthPulseMultiplier = 0.5f;
        public Color DamageVignetteColor => _damageVignetteColor;
        public float DamageVignettePeak => _damageVignettePeak;
        public float DamageFullStrengthHealthFraction => _damageFullStrengthHealthFraction;
        public float DamageFadeSeconds => _damageFadeSeconds;
        public float DamageVignetteSmoothness => _damageVignetteSmoothness;
        public float LowHealthFraction => _lowHealthFraction;
        public float LowHealthVignette => _lowHealthVignette;
        public float LowHealthPulseMultiplier => _lowHealthPulseMultiplier;

        [Header("Old camcorder (provisional)")]
        [SerializeField] private bool _camcorderEnabled = true;
        [SerializeField, Range(0f, 1f)] private float _camcorderCorners = 0.22f;
        [SerializeField, Range(0.01f, 1f)] private float _camcorderCornerRadius = 0.25f;
        [SerializeField, Range(0.01f, 1f)] private float _camcorderCornerSoftness = 0.2f;
        [SerializeField, Range(0f, 4f)] private float _camcorderEdgeBlurPixels = 1.25f;
        [SerializeField, Range(0f, 0.99f)] private float _camcorderEdgeStart = 0.65f;
        [SerializeField, Range(0f, 4f)] private float _tapeJitterPixels = 1.25f;
        [SerializeField, Range(0f, 4f)] private float _tapeChromaPixels = 0.75f;
        [SerializeField, Min(0f)] private float _tapeFrequency = 9f;
        [SerializeField, Min(1f)] private float _tapeLines = 180f;
        [SerializeField, Min(0.001f)] private float _tapeRiseSeconds = 0.08f;
        [SerializeField, Min(0.001f)] private float _tapeRelaxSeconds = 0.65f;
        [SerializeField, Min(0.001f)] private float _tapeHitSeconds = 0.4f;
        [SerializeField, Range(0f, 1f)] private float _tapeCriticalInjury = 0.75f;
        public bool CamcorderEnabled => _camcorderEnabled;
        public float CamcorderCorners => _camcorderCorners;
        public float CamcorderCornerRadius => _camcorderCornerRadius;
        public float CamcorderCornerSoftness => _camcorderCornerSoftness;
        public float CamcorderEdgeBlurPixels => _camcorderEdgeBlurPixels;
        public float CamcorderEdgeStart => _camcorderEdgeStart;
        public float TapeJitterPixels => _tapeJitterPixels;
        public float TapeChromaPixels => _tapeChromaPixels;
        public float TapeFrequency => _tapeFrequency;
        public float TapeLines => _tapeLines;
        public float TapeRiseSeconds => _tapeRiseSeconds;
        public float TapeRelaxSeconds => _tapeRelaxSeconds;
        public float TapeHitSeconds => _tapeHitSeconds;
        public float TapeCriticalInjury => _tapeCriticalInjury;

        [SerializeField] private string[] _blindnessEffectIds = { "blinded" };
        [Header("Presentation upgrades (provisional)")]
        [SerializeField, Range(0f, 1f)] private float _mirrorSkinDurationMultiplier = DefaultMirrorSkinDurationMultiplier;
        [SerializeField, Min(0f)] private float _glimpseSeconds = 0.65f;
        [SerializeField, Range(0f, 0.1f)] private float _glimpseWidth = 0.025f;
        [SerializeField] private Color _glimpseColor = new Color(0.55f, 0.7f, 0.8f, 0.35f);
        public float MirrorSkinDurationMultiplier => _mirrorSkinDurationMultiplier;
        public float GlimpseSeconds => _glimpseSeconds;
        public float GlimpseWidth => _glimpseWidth;
        public Color GlimpseColor => _glimpseColor;
        [SerializeField, Min(0f)] private float _blindTrapSeconds = 2.5f;
        public float BlindTrapSeconds => _blindTrapSeconds;
        [Tooltip("Legacy serialized tuning; grace no longer desaturates on hit.")]
        [SerializeField, Range(-100f, 0f)] private float _graceSaturation = 0f;
        [SerializeField, Min(0.001f)] private float _graceEaseInSeconds = 0.12f;
        [SerializeField, Min(0.001f)] private float _graceEaseOutSeconds = 0.25f;
        public System.Collections.Generic.IReadOnlyList<string> BlindnessEffectIds => _blindnessEffectIds;
        public float GraceSaturation => _graceSaturation;
        public float GraceEaseInSeconds => _graceEaseInSeconds;
        public float GraceEaseOutSeconds => _graceEaseOutSeconds;
        [SerializeField, Range(0f, 1f)] private float _baselineGrain = 0.08f;
        [SerializeField, Range(0f, 1f)] private float _baselineChromatic = 0.025f;
        [Tooltip("Legacy serialized tuning; the URP vignette is now damage-only. Camcorder framing is separate.")]
        [SerializeField, Range(0f, 1f)] private float _frameVignette = 0f;
        [SerializeField, Range(0f, 1f)] private float _subtleIntrusionMultiplier = 0.12f;
        [SerializeField, Range(0f, 1f)] private float _blindnessDarkness = 1f;
        public float BaselineGrain => _baselineGrain;
        public float BaselineChromatic => _baselineChromatic;
        public float FrameVignette => _frameVignette;
        public float SubtleIntrusionMultiplier => _subtleIntrusionMultiplier;
        public float BlindnessDarkness => _blindnessDarkness;
        [SerializeField, Range(0f, 1f)] private float _peripheralChromatic = 0.25f;
        [SerializeField, Range(0f, 0.3f)] private float _peripheralDistortion = 0.12f;
        [Tooltip("Legacy serialized tuning; replaced by the timed red damage border.")]
        [SerializeField, Range(0f, 1f)] private float _injuryVignette = 0.45f;
        [SerializeField] private bool _reacquireBlurEnabled = true;
        [SerializeField, Min(0.001f)] private float _reacquireBlurSeconds = 0.1f;
        [SerializeField, Range(0.5f, 1.5f)] private float _blurRadius = 1f;
        [SerializeField, Range(0f, 100f)] private float _intrusionDesaturation = 70f;
        [SerializeField, Range(0f, 1f)] private float _intrusionGrain = 0.5f;
        [SerializeField, Min(0f)] private float _volumePriority = 20f;

        [SerializeField, Range(0f, 0.8f)] private float _consumptionFadeStart = 0.2f;
        [SerializeField, Range(-10f, 0f)] private float _consumptionExposure = -8f;
        public float ConsumptionFadeStart => _consumptionFadeStart;
        public float ConsumptionExposure => _consumptionExposure;

        public float PeripheralChromatic => _peripheralChromatic;
        public float PeripheralDistortion => _peripheralDistortion;
        public float InjuryVignette => _injuryVignette;
        public bool ReacquireBlurEnabled => _reacquireBlurEnabled;
        public float ReacquireBlurSeconds => _reacquireBlurSeconds;
        public float BlurRadius => _blurRadius;
        public float IntrusionDesaturation => _intrusionDesaturation;
        public float IntrusionGrain => _intrusionGrain;
        public float VolumePriority => _volumePriority;
    }
}

