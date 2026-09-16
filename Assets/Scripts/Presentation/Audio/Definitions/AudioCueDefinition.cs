// ============================================================================
// AudioCueDefinition.cs
// ============================================================================
//
// PURPOSE:
//   Associates an agreed cue identity with its designer-authored playback data.
//   Keeping these values serialized allows prototype sounds to be replaced without code changes.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Carry clip, gain, priority and fade values in the owning DriverConfig.
//   - Expose read-only values to the playback stack.
//
// DEPENDENCIES:
//   - Core CueId; AudioClip is an inert asset reference here.
//
// USAGE NOTES:
//   - No runtime mutations or engine calls occur in this data type.
//   - Missing clips are reported by AudioDriver before arbitration.
//
// ============================================================================

using System;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Audio
{
    [Serializable]
    public struct AudioCueDefinition
    {
        [SerializeField] private CueId _cue;
        [SerializeField] private AudioClip _clip;
        [SerializeField, Range(0f, 1f)] private float _gain;
        [SerializeField, Range(0, 100)] private int _priority;
        [SerializeField, Min(0.001f)] private float _fadeSeconds;

        public AudioCueDefinition(CueId cue, AudioClip clip, float gain, int priority, float fadeSeconds)
        {
            _cue = cue;
            _clip = clip;
            _gain = gain;
            _priority = priority;
            _fadeSeconds = fadeSeconds;
        }
        public CueId Cue => _cue;
        public AudioClip Clip => _clip;
        public float Gain => _gain;
        public int Priority => _priority;
        public float FadeSeconds => _fadeSeconds;
    }
}
