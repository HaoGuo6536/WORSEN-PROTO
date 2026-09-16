// ============================================================================
// EnvironmentDriverConfig.cs
// ============================================================================
// PURPOSE:
//   Gives generated rooms a curated medieval dressing and local pools of light.
//   Imported objects and Lumen effects stay replaceable without changing layout logic.
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Presentation · Environment.
// KEY RESPONSIBILITIES:
//   - Hold imported visual assets, palettes, fake-light strengths and strict effect budgets.
//   - Reference project-owned fake torch and moon profiles; no runtime real lights.
// DEPENDENCIES:
//   - Unity asset references only. Lumen's component API is wrapped by EnvironmentDriver.
// USAGE NOTES:
//   Mirror asset: Resources/ScriptableObjects/Presentation/Environment/EnvironmentDriverConfig.
//   Runtime never modifies this shared asset. Decoration cannot create collision.
// ============================================================================
using UnityEngine;

namespace Worsen.Presentation.Environment
{
    [CreateAssetMenu(fileName = "EnvironmentDriverConfig", menuName = "Worsen/Environment/Driver Config")]
    public sealed class EnvironmentDriverConfig : ScriptableObject
    {
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
