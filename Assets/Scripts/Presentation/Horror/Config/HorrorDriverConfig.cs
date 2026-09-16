// ============================================================================
// HorrorDriverConfig.cs
// ============================================================================
//
// PURPOSE:
//   Defines the dark-room atmosphere, quiet ambience, camera flashlight, and enemy attack warnings.
//   All scene feedback tunables stay in one replaceable asset; runtime changes never edit it.
//
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Presentation · Horror.
//
// KEY RESPONSIBILITIES:
//   - Expose darkness, flashlight, dither fog and attack cue tuning.
//   - Hold the imported growl, optional ambience loop and a build-included warning material.
//   - Reference project-owned Lumen fake-light prefabs and default to black 8–24 meter fog.
//   - Keep a soft, wall-limited close fill that dims when the flashlight is switched off.
//
// DEPENDENCIES:
//   - UnityEngine assets and the Horror system's own settings snapshot.
//
// USAGE NOTES:
//   Mirror asset: Resources/ScriptableObjects/Presentation/Horror/HorrorDriverConfig.
//   Only HorrorDriver and its owned sub-drivers consume these presentation settings.
//   A missing ambience loop is allowed and produces silence without a warning.
//   Legacy spotlight offset/inner-angle/shadow fields remain serialized for asset compatibility;
//   Lumen uses camera-coincident authoritative aim and its profile's soft cone instead.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Presentation.Horror
{
    [CreateAssetMenu(fileName = "HorrorDriverConfig", menuName = "Worsen/Horror/Driver Config")]
    public sealed class HorrorDriverConfig : ScriptableObject
    {
        [Header("Dark rooms")]
        [SerializeField] private Color _ambientColor = new Color(0.14f, 0.16f, 0.16f, 1f);
        [SerializeField] private Color _backgroundColor = Color.black;
        [SerializeField, Range(0f, 1f)] private float _reflectionIntensity = 0.04f;
        [SerializeField] private Color _fogColor = Color.black;
        [SerializeField, Min(0f)] private float _fogNearMeters = 8f;
        [SerializeField, Min(0.01f)] private float _fogFarMeters = 24f;

        [Header("Quiet room ambience")]
        [SerializeField] private AudioClip _ambienceLoop;
        [SerializeField, Range(0f, 1f)] private float _ambienceGain = 0.15f;

        [Header("Camera flashlight")]
        [SerializeField] private GameObject _lumenFlashlightPrefab;
        [SerializeField] private GameObject _lumenNearFillPrefab;
        [SerializeField, Min(0.01f)] private float _flashlightRange = 18f;
        [SerializeField, Min(0f)] private float _flashlightIntensity = 7.5f;
        [SerializeField, Range(1f, 179f)] private float _flashlightSpotAngle = 55f;
        [SerializeField, Range(0f, 179f)] private float _flashlightInnerSpotAngle = 28f;
        [SerializeField] private Color _flashlightColor = new Color(0.86f, 0.93f, 1f, 1f);
        [SerializeField] private Vector3 _flashlightLocalOffset = new Vector3(0.10f, -0.08f, 0.18f);
        [SerializeField, Min(0f)] private float _nearFillIntensity = 0.8f;
        [SerializeField, Min(0.01f)] private float _nearFillRange = 2.6f;
        [SerializeField] private Color _nearFillColor = new Color(0.55f, 0.65f, 0.80f, 1f);
        [SerializeField, Range(0.1f, 5f)] private float _nearFillSmoothness = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _nearFillOffMultiplier = 0.2f;
        [SerializeField, Range(0f, 2f)] private float _flashlightShadowBias = 0.025f;
        [SerializeField, Range(0f, 3f)] private float _flashlightShadowNormalBias = 0.05f;

        [Header("Enemy attack warnings")]
        [SerializeField] private Material _attackMaterial;
        [SerializeField] private AudioClip _attackGrowl;
        [SerializeField, Range(0f, 1f)] private float _attackGain = 0.75f;
        [SerializeField, Min(0.01f)] private float _attackAudioMinDistance = 2f;
        [SerializeField, Min(0.01f)] private float _attackAudioMaxDistance = 20f;
        [SerializeField, Min(0.01f)] private float _attackRadius = 0.62f;
        [SerializeField, Min(0.01f)] private float _windupStartScale = 1.3f;
        [SerializeField, Min(0.01f)] private float _windupEndScale = 0.55f;
        [SerializeField, Min(0.01f)] private float _attackArrowLength = 2.1f;
        [SerializeField, Min(0.01f)] private float _attackArrowHalfWidth = 0.25f;
        [SerializeField, Range(0.01f, 1f)] private float _attackArrowHeadFraction = 0.25f;
        [SerializeField] private float _attackHeight = 0.15f;
        [SerializeField, Min(0.001f)] private float _attackLineWidth = 0.045f;
        [SerializeField, Range(8, 64)] private int _attackRingSegments = 32;
        [SerializeField] private Color _windupColor = new Color(1f, 0.72f, 0.25f, 1f);
        [SerializeField] private Color _activeColor = new Color(1f, 0.10f, 0.035f, 1f);
        [SerializeField] private Color _recoveryColor = new Color(0.45f, 0.22f, 0.08f, 0.65f);

        public Color AmbientColor => _ambientColor;
        public Color BackgroundColor => _backgroundColor;
        public float ReflectionIntensity => _reflectionIntensity;
        public Color FogColor => _fogColor;
        public AudioClip AmbienceLoop => _ambienceLoop;
        public float AmbienceGain => _ambienceGain;
        public float FlashlightSpotAngle => _flashlightSpotAngle;
        public float FlashlightInnerSpotAngle => _flashlightInnerSpotAngle;
        public Color FlashlightColor => _flashlightColor;
        public GameObject LumenFlashlightPrefab => _lumenFlashlightPrefab;
        public GameObject LumenNearFillPrefab => _lumenNearFillPrefab;
        public Vector3 FlashlightLocalOffset => _flashlightLocalOffset;
        public float NearFillIntensity => _nearFillIntensity;
        public float NearFillRange => _nearFillRange;
        public Color NearFillColor => _nearFillColor;
        public float NearFillSmoothness => _nearFillSmoothness;
        public float NearFillOffMultiplier => _nearFillOffMultiplier;
        public float FlashlightShadowBias => _flashlightShadowBias;
        public float FlashlightShadowNormalBias => _flashlightShadowNormalBias;
        public Material AttackMaterial => _attackMaterial;
        public AudioClip AttackGrowl => _attackGrowl;
        public float AttackGain => _attackGain;
        public float AttackAudioMinDistance => _attackAudioMinDistance;
        public float AttackAudioMaxDistance => _attackAudioMaxDistance;
        public float AttackArrowHalfWidth => _attackArrowHalfWidth;
        public float AttackArrowHeadFraction => _attackArrowHeadFraction;
        public float AttackLineWidth => _attackLineWidth;
        public int AttackRingSegments => _attackRingSegments;
        public HorrorPresentationSettings Settings => new HorrorPresentationSettings
        {
            FogNearMeters = _fogNearMeters, FogFarMeters = _fogFarMeters,
            FlashlightRange = _flashlightRange, FlashlightIntensity = _flashlightIntensity,
            AttackRadius = _attackRadius, WindupStartScale = _windupStartScale,
            WindupEndScale = _windupEndScale, AttackArrowLength = _attackArrowLength,
            AttackHeight = _attackHeight, WindupColor = _windupColor,
            ActiveColor = _activeColor, RecoveryColor = _recoveryColor
        };
    }
}
