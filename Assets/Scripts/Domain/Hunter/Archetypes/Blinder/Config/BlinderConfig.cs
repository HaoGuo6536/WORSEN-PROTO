// ============================================================================
// BlinderConfig.cs
// ============================================================================
// PURPOSE:
//   Authors a warned, small-radius blindness projectile and optional curse scales.
//   The existing swept-shot driver is reused only for physics; these independent
//   rules neither create Weaver webs nor enable its ceiling/partition behavior.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Supply provisional attack, sound and bounded curse tuning.
// DEPENDENCIES:
//   - Hunter config base and the parent-owned immutable sweep DriverConfig.
// USAGE NOTES:
//   Four-part pursuit tuning and the depth gate are on HunterProfile.
//   Floor owns trap counts and cadence; More Traps reports capped stacks to it.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Hunter.Archetypes.Blinder
{
    [CreateAssetMenu(menuName = "Worsen/Hunter/Blinder Config")]
    public sealed class BlinderConfig : HunterArchetypeConfig
    {
        [SerializeField] private WeaverDriverConfig _sweepConfig = null;
        [SerializeField, Min(.01f)] private float _range = 16f;
        [SerializeField, Min(.01f)] private float _projectileSpeed = 12f;
        [SerializeField, Range(.01f, .1f)] private float _projectileRadius = .06f;
        [SerializeField, Min(.01f)] private float _warningSeconds = .7f;
        [SerializeField, Min(.01f)] private float _cooldownSeconds = 4f;
        [SerializeField, Min(.01f)] private float _blindSeconds = 3f;
        [SerializeField, Min(1f)] private float _longerDarkMultiplier = 1.5f;
        [SerializeField, Range(1, 8)] private int _curseStackCap = 3;
        [SerializeField, Min(.01f)] private float _presenceInterval = 6f;
        [SerializeField, Min(.01f)] private float _chaseInterval = 2f;
        [SerializeField, Range(0f, 1f)] private float _soundLoudness = .6f;
        [SerializeField, Range(0f, 1f)] private float _trapTickLoudness = .15f;
        public WeaverDriverConfig SweepConfig => _sweepConfig;
        public float Range => Mathf.Max(.01f, _range);
        public float ProjectileSpeed => Mathf.Max(.01f, _projectileSpeed);
        public float ProjectileRadius => Mathf.Clamp(_projectileRadius, .01f, .1f);
        public float WarningSeconds => Mathf.Max(.01f, _warningSeconds);
        public float CooldownSeconds => Mathf.Max(.01f, _cooldownSeconds);
        public float BlindSeconds => Mathf.Max(.01f, _blindSeconds);
        public float LongerDarkMultiplier => Mathf.Max(1f, _longerDarkMultiplier);
        public int CurseStackCap => Mathf.Clamp(_curseStackCap, 1, 8);
        public float PresenceInterval => Mathf.Max(.01f, _presenceInterval);
        public float ChaseInterval => Mathf.Max(.01f, _chaseInterval);
        public float SoundLoudness => Mathf.Clamp01(_soundLoudness);
        public float TrapTickLoudness => Mathf.Clamp01(_trapTickLoudness);
    }
}
