// ============================================================================
// AudioSoundDefinition.cs
// ============================================================================
//
// PURPOSE:
//   Carries authored variation banks and pure voice bookkeeping for the soundscape.
//   These values let the playback boundary choose assets without coupling to gameplay.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Retain per-voice randomized base gain for continuous loop modulation.
//   - Describe spatial attenuation, clip alternatives and bounded overlap.
//   - Carry pure threat and playback values.
//
// DEPENDENCIES:
//   - Core cue identities and value data; own Audio presentation stack only.
//
// USAGE NOTES:
//   Asset references are inert data here; the Driver alone reads clip engine properties.
//
// ============================================================================

using System;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Presentation.Audio
{
    [Serializable]
    public struct AudioSoundDefinition
    {
        public CueId Cue;
        public AudioClip[] Clips;
        [Range(0f, 1f)] public float Gain;
        [Range(0f, 0.5f)] public float GainVariation;
        [Range(0.5f, 2f)] public float PitchMinimum;
        [Range(0.5f, 2f)] public float PitchMaximum;
        [Range(0, 100)] public int Priority;
        [Min(0f)] public float Cooldown;
        [Min(1)] public int MaxConcurrent;
        public bool Spatial;
        public bool Loop;
        public bool Ambience;
        [Min(0.1f)] public float MinimumDistance;
        [Min(1f)] public float MaximumDistance;
    }

    public struct AudioThreatSample
    {
        public bool Chasing;
        public float Closeness;
    }

    public struct AudioVoiceSample
    {
        public int Cue;
        public int Emitter;
        public int Priority;
        public float Remaining;
        public float BaseGain;
        public bool Loop;
    }

    public struct AudioPlaybackSample
    {
        public int Voice;
        public int Clip;
        public float Gain;
        public float Pitch;
        public bool ReuseLoop;
    }
}
