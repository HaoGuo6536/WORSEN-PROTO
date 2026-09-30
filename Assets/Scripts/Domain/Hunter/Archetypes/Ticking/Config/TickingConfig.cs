// ============================================================================
// TickingConfig.cs
// ============================================================================
// PURPOSE:
//   Authors the Ticking's maintenance clock, nearby keys and audible tells.
//   Each spawned clock owns its spring; these values are immutable shared content.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Supply provisional timing, distance and catalogue-effect multipliers.
// DEPENDENCIES:
//   - Hunter archetype config and UnityEngine authoring types only.
// USAGE NOTES:
//   The four-part pursuit brief remains on HunterProfile. Double Spring is a
//   two-key cycle: first key sets half charge, second sets full, even after decay.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter.Archetypes.Ticking
{
    [CreateAssetMenu(menuName = "Worsen/Hunter/Ticking Rules")]
    public sealed class TickingConfig : HunterArchetypeConfig
    {
        [SerializeField, Min(.1f)] private float _springSeconds = 45f;
        [SerializeField, Min(.1f)] private float _keySeconds = 30f;
        [SerializeField] private Vector2 _followDistance = new Vector2(12f, 18f);
        [SerializeField] private Vector2 _keyDistance = new Vector2(6f, 10f);
        [SerializeField, Min(.01f)] private float _fullTickInterval = .35f;
        [SerializeField, Min(.01f)] private float _emptyTickInterval = 2f;
        [SerializeField, Min(1f)] private float _followSpeedRatio = 1.2f;
        [SerializeField, Range(0f, 80f)] private float _behindArcDegrees = 35f;
        [SerializeField, Min(.01f)] private float _placementRetrySeconds = 1f;
        [SerializeField, Range(1, 32)] private int _placementAttempts = 12;
        [SerializeField, Range(.1f, 1f)] private float _runsFasterMultiplier = .8f;
        [SerializeField, Min(1f)] private float _fartherKeysMultiplier = 1.25f;
        [SerializeField, Range(.1f, 1f)] private float _spareKeyMultiplier = .65f;
        [SerializeField, Min(0f)] private float _loudKeyLoudness = 1f;
        [SerializeField] private TickingDriverConfig _driverConfig = null;
        public float SpringSeconds => Mathf.Max(.1f, _springSeconds);
        public float KeySeconds => Mathf.Max(.1f, _keySeconds);
        public Vector2 FollowDistance => new Vector2(Mathf.Max(0f, _followDistance.x), Mathf.Max(_followDistance.x, _followDistance.y));
        public Vector2 KeyDistance => new Vector2(Mathf.Max(0f, _keyDistance.x), Mathf.Max(_keyDistance.x, _keyDistance.y));
        public float FullTickInterval => Mathf.Max(.01f, _fullTickInterval);
        public float EmptyTickInterval => Mathf.Max(FullTickInterval, _emptyTickInterval);
        public float FollowSpeedRatio => Mathf.Max(1f, _followSpeedRatio);
        public float BehindArcDegrees => Mathf.Clamp(_behindArcDegrees, 0f, 80f);
        public float PlacementRetrySeconds => Mathf.Max(.01f, _placementRetrySeconds);
        public int PlacementAttempts => Mathf.Clamp(_placementAttempts, 1, 32);
        public float RunsFasterMultiplier => Mathf.Clamp(_runsFasterMultiplier, .1f, 1f);
        public float FartherKeysMultiplier => Mathf.Max(1f, _fartherKeysMultiplier);
        public float SpareKeyMultiplier => Mathf.Clamp(_spareKeyMultiplier, .1f, 1f);
        public float LoudKeyLoudness => Mathf.Max(0f, _loudKeyLoudness);
        public TickingDriverConfig DriverConfig => _driverConfig;
    }
}
