// ============================================================================
// EchoConfig.cs
// ============================================================================
// PURPOSE:
//   Authors the Echo's provisional recording, contact, cue and curse rules.
//   All multipliers are neutral without active effects; the shared profile holds
//   inertia, attack commitment and the playback speed ratio.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Bound recording memory, per-effect stacks and Trail Reader output.
// DEPENDENCIES:
//   - Hunter config base and UnityEngine asset authoring only.
// USAGE NOTES:
//   Resources: ScriptableObjects/Domain/Hunter/Archetypes/Echo/EchoConfig.
//   Samples every injected tick (fixed-rate Session tick), never wall-clock time.
//   Loss is unconditionally NeverLoses; changing it would change the archetype rule.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter.Archetypes.Echo
{
    [CreateAssetMenu(menuName = "Worsen/Hunter/Echo Config")]
    public sealed class EchoConfig : HunterArchetypeConfig
    {
        public override bool NeverLoses => true;
        [SerializeField, Min(0f)] private float _delaySeconds = 4f;
        [SerializeField, Min(4)] private int _sampleCapacity = 4096;
        [SerializeField, Min(0f)] private float _contactRadius = 0.9f;
        [SerializeField, Min(0f)] private float _contactElevation = 0.45f;
        [SerializeField, Min(0f)] private float _pathTolerance = 0.001f;
        [SerializeField, Range(0f, 1f)] private float _footstepGain = 1f;
        [SerializeField, Min(0.01f)] private float _footstepPitch = 0.94f;
        [SerializeField, Min(0f)] private float _trailSeconds = 2f;
        [SerializeField, Min(2)] private int _trailPointLimit = 256;
        [SerializeField, Range(0.01f, 1f)] private float _shorterDelayMultiplier = 0.75f;
        [SerializeField, Min(1f)] private float _fasterPlaybackMultiplier = 1.25f;
        [SerializeField, Range(0f, 1f)] private float _silentStepsMultiplier = 0.5f;
        [SerializeField, Range(1, 8)] private int _curseStackCap = 3;
        public float DelaySeconds => Mathf.Max(0f, _delaySeconds);
        public int SampleCapacity => Mathf.Max(4, _sampleCapacity);
        public float ContactRadius => Mathf.Max(0f, _contactRadius);
        public float ContactElevation => Mathf.Max(0f, _contactElevation);
        public float PathTolerance => Mathf.Max(0.000001f, _pathTolerance);
        public float FootstepGain => Mathf.Clamp01(_footstepGain);
        public float FootstepPitch => Mathf.Max(0.01f, _footstepPitch);
        public float TrailSeconds => Mathf.Max(0f, _trailSeconds);
        public int TrailPointLimit => Mathf.Max(2, _trailPointLimit);
        public float ShorterDelayMultiplier => Mathf.Clamp(_shorterDelayMultiplier, 0.01f, 1f);
        public float FasterPlaybackMultiplier => Mathf.Max(1f, _fasterPlaybackMultiplier);
        public float SilentStepsMultiplier => Mathf.Clamp01(_silentStepsMultiplier);
        public int CurseStackCap => Mathf.Clamp(_curseStackCap, 1, 8);
    }
}
