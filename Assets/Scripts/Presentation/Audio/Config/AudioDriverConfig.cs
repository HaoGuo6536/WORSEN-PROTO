// ============================================================================
// AudioDriverConfig.cs
// ============================================================================
//
// PURPOSE:
//   Holds the presentation-only tuning and clip references for the audio service.
//   Designers can replace the audible prototype samples while preserving mixer behavior.
//
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Provide explicit cue priorities, gains, fade durations and layer clips.
//   - Expose proximity, critical breathing and footstep cadence tuning read-only.
//
// DEPENDENCIES:
//   - Core CueId in own AudioCueDefinition values; no gameplay systems.
//
// USAGE NOTES:
//   - Asset mirrors Presentation/Audio under Resources/ScriptableObjects.
//   - AudioConfigGenerator fills missing original prototype clip references.
//   - Runtime reads settings without mutating the shared asset.
//
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Audio
{
    [CreateAssetMenu(fileName = "AudioDriverConfig", menuName = "Worsen/Audio/Driver Config")]
    public sealed class AudioDriverConfig : ScriptableObject
    {
        [SerializeField, Range(0f, 1f)] private float _masterGain = 0.8f;
        [SerializeField, Min(0.001f)] private float _layerFadeSeconds = 0.3f;
        [SerializeField, Range(0f, 1f)] private float _breathMaximumGain = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _hunterMaximumGain = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _criticalBreathGain = 0.55f;
        [SerializeField, Range(0f, 1f)] private float _criticalHealthFraction = 0.25f;
        [SerializeField, Min(0.01f)] private float _slowStepSeconds = 0.6f;
        [SerializeField, Min(0.01f)] private float _fastStepSeconds = 0.28f;
        [SerializeField, Range(0f, 1f)] private float _minimumStepSpeed = 0.04f;
        [SerializeField] private AudioClip _breathLoop;
        [SerializeField] private AudioClip _hunterLoop;
        [SerializeField] private AudioCueDefinition[] _cues =
        {
            new AudioCueDefinition(CueId.Presence, null, 0.55f, 10, 0.08f),
            new AudioCueDefinition(CueId.Detection, null, 0.75f, 70, 0.025f),
            new AudioCueDefinition(CueId.Chase, null, 0.8f, 80, 0.025f),
            new AudioCueDefinition(CueId.Lose, null, 0.55f, 80, 0.08f),
            new AudioCueDefinition(CueId.Death, null, 0.9f, 100, 0.025f),
            new AudioCueDefinition(CueId.Footstep, null, 0.3f, 5, 0.015f),
            new AudioCueDefinition(CueId.ExitOpen, null, 0.55f, 65, 0.08f),
            new AudioCueDefinition(CueId.RoomTelegraph, null, 0.55f, 50, 0.08f)
        };

        public float MasterGain => _masterGain;
        public AudioClip BreathLoop => _breathLoop;
        public AudioClip HunterLoop => _hunterLoop;
        public IReadOnlyList<AudioCueDefinition> Cues => _cues;
        public AudioMixSettings MixSettings => new AudioMixSettings
        {
            LayerFadeSeconds = _layerFadeSeconds, BreathMaximumGain = _breathMaximumGain,
            HunterMaximumGain = _hunterMaximumGain, CriticalBreathGain = _criticalBreathGain,
            CriticalHealthFraction = _criticalHealthFraction, SlowStepSeconds = _slowStepSeconds,
            FastStepSeconds = _fastStepSeconds, MinimumStepSpeed = _minimumStepSpeed
        };
    }
}
