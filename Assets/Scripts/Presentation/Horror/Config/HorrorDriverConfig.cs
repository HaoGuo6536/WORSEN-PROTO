// ============================================================================
// HorrorDriverConfig.cs
// ============================================================================
//
// PURPOSE:
//   Defines the dark-room atmosphere, near-silent ambience, camera flashlight, and enemy attack warnings.
//   All scene feedback tunables stay in one replaceable asset; runtime changes never edit it.
//
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Presentation · Horror.
//
// KEY RESPONSIBILITIES:
//   - Tune Weaver warnings, micro-events and the earned-startle budget.
//   - Bind catalogue lighting effects and supply Afterglow visual strengths.
//   - Hold authored audio, warning materials and Lumen fake-light prefabs.
//   - Guarantee a cool ambient floor and visible, independently dithered atmospheric haze.
//   - Tune the flashlight and its wall-limited close fill.
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
//   Fallback attack and web shaders are serialized by HorrorShaderSetup for player builds.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Presentation.Horror
{
    [CreateAssetMenu(fileName = "HorrorDriverConfig", menuName = "Worsen/Horror/Driver Config")]
    public sealed class HorrorDriverConfig : ScriptableObject
    {
        [SerializeField] private Shader _attackShader = null;
        [SerializeField] private Shader _webShader = null;
        public Shader AttackShader => _attackShader;
        public Shader WebShader => _webShader;
        [Header("Deep dark in collapse (provisional)")]
        [SerializeField, Min(0f)] private float _sweepFogNearMeters = 6f;
        [SerializeField, Min(0f)] private float _collapsedFogNearMeters = 4f;
        [SerializeField, Range(0f, 1f)] private float _encroachingCollapseWeight = 0.5f;
        [SerializeField, Min(0f)] private float _collapseSmoothingSeconds = 2f;
        [SerializeField, Range(0f, 1f)] private float _sweepTorchCountMultiplier = 1f;
        [SerializeField, Range(0f, 1f)] private float _collapsedTorchCountMultiplier = 1f;
        public float SweepFogNearMeters => Mathf.Clamp(_sweepFogNearMeters, 4f, 6f);
        public float CollapsedFogNearMeters => Mathf.Clamp(_collapsedFogNearMeters, 4f, 6f);
        public float EncroachingCollapseWeight => _encroachingCollapseWeight;
        public float CollapseSmoothingSeconds => _collapseSmoothingSeconds;
        public float SweepTorchCountMultiplier => _sweepTorchCountMultiplier;
        public float CollapsedTorchCountMultiplier => _collapsedTorchCountMultiplier;
        [Header("Afterglow presentation (provisional; safety time is gameplay-owned)")]
        [SerializeField, Min(0f)] private float _afterglowStrength = 0.35f;
        [SerializeField, Min(0.01f)] private float _afterglowRadius = 2f;
        [SerializeField] private Color _afterglowColor = new Color(0.55f, 0.65f, 0.8f, 1f);
        public float AfterglowStrength => _afterglowStrength;
        public float AfterglowRadius => _afterglowRadius;
        public Color AfterglowColor => _afterglowColor;
        [Header("Weaver placeholders (provisional)")]
        [SerializeField] private Material _webMaterial = null;
        [SerializeField, Min(.001f)] private float _webLineWidth = .015f;
        [SerializeField, Min(.1f)] private float _webDoorHeight = 2.2f;
        [SerializeField] private Color _webWarningColor = new Color(.65f, .8f, .75f, .55f);
        [SerializeField] private Color _webGlowColor = new Color(.3f, .5f, .4f, .12f);
        public Material WebMaterial => _webMaterial;
        public float WebLineWidth => _webLineWidth;
        public float WebDoorHeight => _webDoorHeight;
        public Color WebWarningColor => _webWarningColor;
        public Color WebGlowColor => _webGlowColor;
        [Header("Micro-events (independent of loud startles)")]
        [SerializeField, Min(0)] private int _microEventsPerRun = 2;
        [SerializeField, Min(60f)] private float _microEventSpacingSeconds = 60f;
        [SerializeField, Min(1f)] private float _microEventMeanWaitSeconds = 300f;
        [SerializeField, Min(0f)] private float _silhouetteSeconds = 1f;
        [SerializeField, Min(0f)] private float _counterCakeSeconds = 0.7f;
        [SerializeField, Min(0f)] private float _microEventMinimumDistance = 6f;
        [SerializeField, Range(0f, 0.5f)] private float _silhouetteEdgeFraction = 0.12f;
        [SerializeField] private Vector3 _silhouetteScale = new Vector3(0.45f, 0.9f, 0.45f);
        [SerializeField] private string[] _darkerFloorEffectIds = { "darker-floors" };
        [SerializeField] private string[] _catEyesEffectIds = { "cat-eyes" };
        [SerializeField] private string[] _wickEffectIds = { "wick" };
        [SerializeField, Range(0f, 1f)] private float _darkerTorchCountMultiplier = 0.5f;
        public int MicroEventsPerRun => _microEventsPerRun;
        public float MicroEventSpacingSeconds => _microEventSpacingSeconds;
        public float MicroEventMeanWaitSeconds => _microEventMeanWaitSeconds;
        public float SilhouetteSeconds => _silhouetteSeconds;
        public float CounterCakeSeconds => _counterCakeSeconds;
        public float MicroEventMinimumDistance => _microEventMinimumDistance;
        public float SilhouetteEdgeFraction => _silhouetteEdgeFraction;
        public Vector3 SilhouetteScale => _silhouetteScale;
        public System.Collections.Generic.IReadOnlyList<string> DarkerFloorEffectIds => _darkerFloorEffectIds;
        public System.Collections.Generic.IReadOnlyList<string> CatEyesEffectIds => _catEyesEffectIds;
        public System.Collections.Generic.IReadOnlyList<string> WickEffectIds => _wickEffectIds;
        public float DarkerTorchCountMultiplier => _darkerTorchCountMultiplier;
        [Header("Presentation startle budget")]
        [SerializeField, Min(0)] private int _startlesPerRun = 2;
        [SerializeField, Min(0f)] private float _startleSpacingSeconds = 120f;
        [SerializeField, Range(0f, 1f)] private float _earnedStartleChance = 1f;
        [Header("Default-off effect hook strengths")]
        [SerializeField, Range(0.01f, 1f)] private float _darkerFogDistanceMultiplier = 0.7f;
        [SerializeField, Min(1f)] private float _catEyesFogStartMultiplier = 1.5f;
        public int StartlesPerRun => _startlesPerRun;
        public float StartleSpacingSeconds => _startleSpacingSeconds;
        public float EarnedStartleChance => _earnedStartleChance;
        public float DarkerFogDistanceMultiplier => _darkerFogDistanceMultiplier;
        public float CatEyesFogStartMultiplier => _catEyesFogStartMultiplier;
        [Header("Dark rooms")]
        [SerializeField] private Color _ambientColor = new Color(.10f, .13f, .17f, 1f);
        [SerializeField] private Color _minimumAmbientColor = new Color(.09f, .115f, .15f, 1f);
        [SerializeField, Min(0f)] private float _minimumFillIntensity = .16f;
        [SerializeField] private Color _minimumFogColor = new Color(.07f, .09f, .105f, 1f);
        [SerializeField, Range(.1f, .85f)] private float _fogOpacity = .72f;
        [SerializeField, Range(1, 8)] private int _ditherScale = 2;
        public float MinimumFillIntensity => _minimumFillIntensity;
        public float FogOpacity => Mathf.Clamp(_fogOpacity, .1f, .85f);
        public int DitherScale => Mathf.Clamp(_ditherScale, 1, 8);
        [SerializeField] private Color _backgroundColor = Color.black;
        [SerializeField, Range(0f, 1f)] private float _reflectionIntensity = 0.04f;
        [SerializeField] private Color _fogColor = new Color(.085f, .11f, .125f, 1f);
        [SerializeField, Min(0f)] private float _fogNearMeters = 6f;
        [SerializeField, Min(0.01f)] private float _fogFarMeters = 30f;

        [Header("Quiet room ambience")]
        [SerializeField] private AudioClip _ambienceLoop;
        [SerializeField, Range(0f, 1f)] private float _ambienceGain = 0.01f;

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
        [SerializeField, Min(0.01f)] private float _nearFillRange = 3.2f;
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

        public Color AmbientColor => HorrorVisibilityPresenter.AtLeast(_ambientColor, _minimumAmbientColor);
        public Color BackgroundColor => _backgroundColor;
        public float ReflectionIntensity => _reflectionIntensity;
        public Color FogColor => HorrorVisibilityPresenter.AtLeast(_fogColor, _minimumFogColor);
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
            FogNearMeters = Mathf.Clamp(_fogNearMeters, 4f, 6f), FogFarMeters = Mathf.Clamp(_fogFarMeters, 25f, 35f),
            FlashlightRange = _flashlightRange, FlashlightIntensity = _flashlightIntensity,
            AttackRadius = _attackRadius, WindupStartScale = _windupStartScale,
            WindupEndScale = _windupEndScale, AttackArrowLength = _attackArrowLength,
            AttackHeight = _attackHeight, WindupColor = _windupColor,
            ActiveColor = _activeColor, RecoveryColor = _recoveryColor
        };
    }
}
