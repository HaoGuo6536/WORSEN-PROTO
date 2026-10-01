// ============================================================================
// EnvironmentDriverConfig.cs
// ============================================================================
// PURPOSE:
//   Gives generated rooms a curated medieval dressing and local pools of light.
//   Imported objects and Lumen effects stay replaceable without changing layout logic.
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Presentation · Environment.
// KEY RESPONSIBILITIES:
//   - Tune mesh-based placement and kit fixtures with restrained fluorescent flicker.
//   - Hold imported visual assets, palettes, fake-light strengths and strict effect budgets.
//   - Reference project-owned fake torch and moon profiles; no runtime real lights.
//   - Tune restrained lantern halos, ground pools, exit fans, thin fog and optional hunter rim.
// DEPENDENCIES:
//   - Unity asset references only. Lumen's component API is wrapped by EnvironmentDriver.
// USAGE NOTES:
//   Mirror asset: Resources/ScriptableObjects/Presentation/Environment/EnvironmentDriverConfig.
//   Runtime never modifies this shared asset. Decoration cannot create collision.
//   Chalk and panel shaders must be serialized by deterministic world setup.
// ============================================================================
using UnityEngine;

namespace Worsen.Presentation.Environment
{
    [CreateAssetMenu(fileName = "EnvironmentDriverConfig", menuName = "Worsen/Environment/Driver Config")]
    public sealed class EnvironmentDriverConfig : ScriptableObject
    {
        public const float DefaultMinimumPropScale = .75f;
        public const float DefaultMaximumPropScale = 1.25f;
        public static readonly Vector3 DefaultFloorEnvelope = new Vector3(2.5f, 2.4f, 1.1f);
        public static readonly Vector3 DefaultWallEnvelope = new Vector3(2f, 2.8f, .6f);
        [SerializeField] private float _minimumPropScale = DefaultMinimumPropScale;
        [SerializeField] private float _maximumPropScale = DefaultMaximumPropScale;
        [SerializeField] private Vector3 _floorEnvelope = DefaultFloorEnvelope;
        [SerializeField] private Vector3 _wallEnvelope = DefaultWallEnvelope;
        [SerializeField] private Vector3 _fixtureEnvelope = new Vector3(2f, 1.2f, 1.2f);
        [SerializeField] private string _castleFurniture = "ProceduralKits/Castle/prop_bench";
        [SerializeField] private string _castleWallDecoration = "ProceduralKits/Castle/prop_banner";
        [SerializeField] private string _hospitalFurniture = "ProceduralKits/Hospital/prop_waiting_bench";
        [SerializeField] private string _schoolFurniture = "ProceduralKits/School/prop_bookcase";
        [SerializeField] private string _basementFurniture = "ProceduralKits/Basement/prop_electrical_cabinet";
        [SerializeField] private string _hospitalFixture = "ProceduralKits/Hospital/light_fluorescent_panel";
        [SerializeField] private string _schoolFixture = "ProceduralKits/School/prop_fluorescent";
        [SerializeField] private string _basementFixture = "ProceduralKits/Basement/cage_lamp";
        [SerializeField, Min(0f)] private float _fixtureEmission = 1.5f;
        public float MinimumPropScale => _minimumPropScale;
        public float MaximumPropScale => _maximumPropScale;
        public Vector3 FloorEnvelope => _floorEnvelope;
        public Vector3 WallEnvelope => _wallEnvelope;
        public Vector3 FixtureEnvelope => _fixtureEnvelope;
        public string CastleFurniture => _castleFurniture;
        public string CastleWallDecoration => _castleWallDecoration;
        public string HospitalFurniture => _hospitalFurniture;
        public string SchoolFurniture => _schoolFurniture;
        public string BasementFurniture => _basementFurniture;
        public string HospitalFixture => _hospitalFixture;
        public string SchoolFixture => _schoolFixture;
        public string BasementFixture => _basementFixture;
        public float FixtureEmission => _fixtureEmission;
        [SerializeField] private Shader _chalkShader = null;
        [SerializeField] private Shader _panelShader = null;
        public Shader ChalkShader => _chalkShader;
        public Shader PanelShader => _panelShader;
        [SerializeField] private Color _fluorescentColor = new Color(0.65f, 0.85f, 1f);
        [SerializeField] private Vector3 _fluorescentPanelSize = new Vector3(0.5f, 0.8f, 0.08f);
        [SerializeField, Range(0f, 0.2f)] private float _fluorescentFlickerDepth = 0.04f;
        [SerializeField, Min(0f)] private float _fluorescentFlickerRate = 9f;
        public Color FluorescentColor => _fluorescentColor;
        public Vector3 FluorescentPanelSize => _fluorescentPanelSize;
        public float FluorescentFlickerDepth => Mathf.Clamp(_fluorescentFlickerDepth, 0f, 0.2f);
        public float FluorescentFlickerRate => Mathf.Max(0f, _fluorescentFlickerRate);
        [SerializeField, Range(0f, 1f)] private float _lanternHaloStrength = 0.18f;
        [SerializeField, Min(0f)] private float _groundPoolStrength = 0.35f;
        [SerializeField, Min(0.01f)] private float _groundPoolRadius = 4f;
        [SerializeField, Range(0.1f, 5f)] private float _groundPoolSoftness = 3f;
        [SerializeField, Range(1f, 179f)] private float _groundPoolCone = 130f;
        [SerializeField, Range(1, 9)] private int _exitRayCount = 5;
        [SerializeField, Range(0f, 80f)] private float _exitRaySpread = 30f;
        [SerializeField] private Vector2 _exitRaySize = new Vector2(0.45f, 4f);
        [SerializeField, Min(0f)] private float _exitRayClosedIntensity = 0.04f;
        [SerializeField, Min(0f)] private float _exitRayOpenIntensity = 0.65f;
        [SerializeField, Range(0f, 1f)] private float _thinFogLimit = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _fogBoundaryStrength = 0.08f;
        [SerializeField] private bool _hunterRimEnabled = false;
        [SerializeField, Range(0f, 1f)] private float _hunterRimStrength = 0.08f;
        [SerializeField, Range(0f, 1f)] private float _darkerLightMultiplier = 0.65f;
        public float LanternHaloStrength => _lanternHaloStrength;
        public float GroundPoolStrength => _groundPoolStrength;
        public float GroundPoolRadius => _groundPoolRadius;
        public float GroundPoolSoftness => _groundPoolSoftness;
        public float GroundPoolCone => _groundPoolCone;
        public int ExitRayCount => _exitRayCount;
        public float ExitRaySpread => _exitRaySpread;
        public Vector2 ExitRaySize => _exitRaySize;
        public float ExitRayClosedIntensity => _exitRayClosedIntensity;
        public float ExitRayOpenIntensity => _exitRayOpenIntensity;
        public float ThinFogLimit => _thinFogLimit;
        public float FogBoundaryStrength => _fogBoundaryStrength;
        public bool HunterRimEnabled => _hunterRimEnabled;
        public float HunterRimStrength => _hunterRimStrength;
        public float DarkerLightMultiplier => _darkerLightMultiplier;
        [SerializeField] private GameObject _wallTorchPrefab;
        [SerializeField] private GameObject _firePrefab;
        [SerializeField] private GameObject[] _wallDecorationPrefabs;
        [SerializeField] private GameObject _doorArchPrefab;
        [SerializeField] private GameObject _cornerColumnPrefab;
        [SerializeField] private GameObject[] _floorPropPrefabs;
        [SerializeField] private GameObject _merchantDisplayPrefab;
        [SerializeField] private GameObject _lumenLanternPrefab;
        [SerializeField] private GameObject _lumenMoonPrefab;
        // Legacy serialization retained for older setup tools; runtime uses no real lights.
        [SerializeField] private Light _lightTemplate;
        [SerializeField] private Color _warmColor = new Color(1f, 0.57f, 0.25f);
        [SerializeField] private Color _moonColor = new Color(0.34f, 0.52f, 0.8f);
        [SerializeField, Min(0f)] private float _torchIntensity = 0.7f;
        [SerializeField, Min(1f)] private float _lightRange = 7f;
        [SerializeField, Range(1, 24)] private int _maximumLumenEffects = 12;
        [SerializeField, Range(1, 12)] private int _maximumRealtimeLights = 6;
        [SerializeField, Range(0, 4)] private int _maximumShadowLights = 2;
        [SerializeField, Min(1f)] private float _effectDistance = 60f;
        [SerializeField, Min(0.05f)] private float _refreshInterval = 0.1f;
        [SerializeField, Min(0.01f)] private float _lumenRangeMultiplier = 1f;
        [SerializeField, Min(0f)] private float _lumenBrightness = 0.95f;
        [SerializeField, Min(0.01f)] private float _lumenFlareScale = 0.7f;
        public float LumenRangeMultiplier => _lumenRangeMultiplier;
        public float LumenBrightness => _lumenBrightness;
        public float LumenFlareScale => _lumenFlareScale;
        public GameObject WallTorchPrefab => _wallTorchPrefab;
        public GameObject FirePrefab => _firePrefab;
        public GameObject[] WallDecorationPrefabs => _wallDecorationPrefabs;
        public GameObject DoorArchPrefab => _doorArchPrefab;
        public GameObject CornerColumnPrefab => _cornerColumnPrefab;
        public GameObject[] FloorPropPrefabs => _floorPropPrefabs;
        public GameObject MerchantDisplayPrefab => _merchantDisplayPrefab;
        public GameObject LumenLanternPrefab => _lumenLanternPrefab;
        public GameObject LumenMoonPrefab => _lumenMoonPrefab;
        public Light LightTemplate => _lightTemplate;
        public Color WarmColor => _warmColor;
        public Color MoonColor => _moonColor;
        public float TorchIntensity => _torchIntensity;
        public float LightRange => _lightRange;
        public int MaximumLumenEffects => _maximumLumenEffects;
        public int MaximumRealtimeLights => _maximumRealtimeLights;
        public int MaximumShadowLights => _maximumShadowLights;
        public float EffectDistance => _effectDistance;
        public float RefreshInterval => _refreshInterval;
    }
}
