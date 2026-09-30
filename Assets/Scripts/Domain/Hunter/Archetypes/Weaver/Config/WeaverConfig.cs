// ============================================================================
// WeaverConfig.cs
// ============================================================================
// PURPOSE:
//   Authors the Weaver's warned web attack and persistent doorway habit. Neutral
//   defaults keep every curse optional; the HunterProfile supplies the four-part
//   inertia, commitment, speed ratio and loss brief rather than duplicating it.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Bound projectile size, warning, effect stacks and per-life web storage.
// DEPENDENCIES:
//   - Hunter config base and UnityEngine authoring values only.
// USAGE NOTES:
//   Provisional values. Resources: ScriptableObjects/Domain/Hunter/Archetypes/Weaver.
//   Geometry is passed as commands to the sub-driver, which has no own tuning.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter.Archetypes.Weaver
{
    [CreateAssetMenu(menuName = "Worsen/Hunter/Weaver Config")]
    public sealed class WeaverConfig : HunterArchetypeConfig
    {
        [SerializeField] private WeaverDriverConfig _driverConfig = null;
        public WeaverDriverConfig DriverConfig => _driverConfig;
        [SerializeField, Min(0.01f)] private float _warningSeconds = .7f;
        [SerializeField, Min(0.01f)] private float _shotCooldownSeconds = 3f;
        [SerializeField, Range(0.01f, .1f)] private float _projectileRadius = .06f;
        [SerializeField, Min(0.1f)] private float _projectileSpeed = 11f;
        [SerializeField, Min(1f)] private float _shotRange = 15f;
        [SerializeField, Range(0f, 1f)] private float _slowMultiplier = .55f;
        [SerializeField, Min(0.01f)] private float _slowSeconds = 2.5f;
        [SerializeField, Min(1f)] private float _stickierMultiplier = 1.5f;
        [SerializeField, Min(1f)] private float _widerMultiplier = 1.25f;
        [SerializeField, Range(.01f, 1f)] private float _quickSpinMultiplier = .75f;
        [SerializeField, Range(1, 8)] private int _curseStackCap = 3;
        [SerializeField, Range(0f, 1f)] private float _nestChancePerStack = .25f;
        [SerializeField, Min(1)] private int _maximumNests = 16;
        [SerializeField, Min(.01f)] private float _nestRadius = .7f;
        [SerializeField, Min(.01f)] private float _nestLifetimeSeconds = 20f;
        [SerializeField, Min(.01f)] private float _skitterIntervalSeconds = 1.2f;
        public float WarningSeconds => Mathf.Max(.01f, _warningSeconds);
        public float ShotCooldownSeconds => Mathf.Max(.01f, _shotCooldownSeconds);
        public float ProjectileRadius => Mathf.Clamp(_projectileRadius, .01f, .1f);
        public float ProjectileSpeed => Mathf.Max(.1f, _projectileSpeed);
        public float ShotRange => Mathf.Max(1f, _shotRange);
        public float SlowMultiplier => Mathf.Clamp01(_slowMultiplier);
        public float SlowSeconds => Mathf.Max(.01f, _slowSeconds);
        public float StickierMultiplier => Mathf.Max(1f, _stickierMultiplier);
        public float WiderMultiplier => Mathf.Max(1f, _widerMultiplier);
        public float QuickSpinMultiplier => Mathf.Clamp(_quickSpinMultiplier, .01f, 1f);
        public int CurseStackCap => Mathf.Clamp(_curseStackCap, 1, 8);
        public float NestChancePerStack => Mathf.Clamp01(_nestChancePerStack);
        public int MaximumNests => Mathf.Max(1, _maximumNests);
        public float NestRadius => Mathf.Max(.01f, _nestRadius);
        public float NestLifetimeSeconds => Mathf.Max(.01f, _nestLifetimeSeconds);
        public float SkitterIntervalSeconds => Mathf.Max(.01f, _skitterIntervalSeconds);
    }
}
