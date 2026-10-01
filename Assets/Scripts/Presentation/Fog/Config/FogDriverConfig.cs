// ============================================================================
// FogDriverConfig.cs
// ============================================================================
// PURPOSE:
//   Defines readable doorway haze and the retained opt-in volumetric spike controls.
//   Doorway mode uses exact geometry instead of a leaking, interpolated density field.
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Presentation · Fog.
// KEY RESPONSIBILITIES:
//   - Define light grey palettes and a transparent doorway layer independently of hazards.
//   - Bound texture memory, update frequency, portal shape and absorption.
// DEPENDENCIES:
//   - UnityEngine serialized values only.
// USAGE NOTES:
//   Asset lives at Resources/ScriptableObjects/Presentation/Fog/FogDriverConfig.
//   Grid limits and march count are clamped to the spike's hard safety ceilings.
// ============================================================================
using UnityEngine;

namespace Worsen.Presentation.Fog
{
    [CreateAssetMenu(menuName = "Worsen/Fog/Driver Config")]
    public sealed class FogDriverConfig : ScriptableObject
    {
        [SerializeField] private bool _doorwayOnly = true;
        [SerializeField] private Shader _doorwayShader = null;
        [SerializeField, Range(0f, .5f)] private float _doorwayOpacity = .22f;
        [SerializeField, Range(0f, .1f)] private float _doorwayInset = .02f;
        public bool DoorwayOnly => _doorwayOnly;
        public Shader DoorwayShader => _doorwayShader;
        public float DoorwayOpacity => Mathf.Clamp(_doorwayOpacity, 0f, .5f);
        public float DoorwayInset => Mathf.Clamp(_doorwayInset, 0f, .1f);
        [SerializeField, Min(.01f)] private float _horizontalVoxel = 1f;
        [SerializeField, Min(.01f)] private float _verticalVoxel = .75f;
        [SerializeField] private Vector3Int _gridCap = new Vector3Int(128, 16, 128);
        [SerializeField, Range(0f, .1f)] private float _progressEpsilon = .01f;
        [SerializeField, Min(.001f)] private float _portalMatchTolerance = .05f;
        [SerializeField, Min(.1f)] private float _portalWidth = 3.2f;
        [SerializeField, Min(.1f)] private float _portalHeight = 2.8f;
        [SerializeField, Range(.01f, 1f)] private float _mouthSoftness = .25f;
        [SerializeField, Min(.1f)] private float _leakDistance = 4f;
        [SerializeField, Range(0f, 2f)] private float _leakGrowth = 1f;
        [SerializeField, Min(.1f)] private float _verticalFalloff = 1.5f;
        [SerializeField, Range(0f, 1f)] private float _tendrilStrength = .35f;
        [SerializeField, Min(.1f)] private float _tendrilWavelength = 3f;
        [SerializeField] private Color _bodyColor = new Color(.62f, .65f, .68f, 1f);
        [SerializeField] private Color _coldBodyColor = new Color(.58f, .66f, .7f, 1f);
        [SerializeField] private Color _coldThinColor = new Color(.22f, .4f, .5f, 1f);
        public Color ColdBodyColor => _coldBodyColor;
        public Color ColdThinColor => _coldThinColor;
        [SerializeField] private Color _thinColor = new Color(.18f, .32f, .45f, 1f);
        [SerializeField, Min(0f)] private float _glowIntensity = .06f;
        [SerializeField, Range(.01f, .9f)] private float _thinThreshold = .25f;
        [SerializeField, Min(0f)] private float _extinction = 4f;
        [SerializeField, Min(0f)] private float _intensity = 1f;
        [SerializeField, Range(1, 48)] private int _maxSteps = 48;
        [SerializeField, Range(.0001f, .1f)] private float _earlyExit = .01f;
        public float HorizontalVoxel => Mathf.Max(.01f, _horizontalVoxel);
        public float VerticalVoxel => Mathf.Max(.01f, _verticalVoxel);
        public Vector3Int GridCap => new Vector3Int(Mathf.Clamp(_gridCap.x, 1, 128), Mathf.Clamp(_gridCap.y, 1, 16), Mathf.Clamp(_gridCap.z, 1, 128));
        public float ProgressEpsilon => Mathf.Clamp(_progressEpsilon, 0f, .1f);
        public float PortalMatchTolerance => Mathf.Max(.001f, _portalMatchTolerance);
        public float PortalWidth => Mathf.Max(.1f, _portalWidth);
        public float PortalHeight => Mathf.Max(.1f, _portalHeight);
        public float MouthSoftness => Mathf.Clamp(_mouthSoftness, .01f, 1f);
        public float LeakDistance => Mathf.Max(.1f, _leakDistance);
        public float LeakGrowth => Mathf.Clamp(_leakGrowth, 0f, 2f);
        public float VerticalFalloff => Mathf.Max(.1f, _verticalFalloff);
        public float TendrilStrength => Mathf.Clamp01(_tendrilStrength);
        public float TendrilWavelength => Mathf.Max(.1f, _tendrilWavelength);
        public Color BodyColor => _bodyColor;
        public Color ThinColor => _thinColor;
        public float GlowIntensity => Mathf.Max(0f, _glowIntensity);
        public float ThinThreshold => Mathf.Clamp(_thinThreshold, .01f, .9f);
        public float Extinction => Mathf.Max(0f, _extinction);
        public float Intensity => Mathf.Max(0f, _intensity);
        public int MaxSteps => Mathf.Clamp(_maxSteps, 1, 48);
        public float EarlyExit => Mathf.Clamp(_earlyExit, .0001f, .1f);
    }
}
