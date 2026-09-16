// ============================================================================
// AudioSoundscapeDriverState.cs
// ============================================================================
//
// PURPOSE:
//   Retains only transient soundscape mixing and voice allocation data.
//   A complete reset clears threat ownership, variation history and loop admission between runs.
//
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Store per-emitter cooldowns, pooled voice leases and threat samples.
//   - Keep musical envelopes and presentation random source outside the Presenter.
//
// DEPENDENCIES:
//   - Core cue identities and value data; own Audio presentation stack only.
//
// USAGE NOTES:
//   Owned by AudioSoundscapeDriver; never exposed to gameplay.
//   Cosmetic randomness is independent of the run seed and is injected into calculations.
//
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Worsen.Presentation.Audio
{
    public sealed class AudioSoundscapeDriverState
    {
        public readonly Dictionary<int, AudioThreatSample> Threats = new Dictionary<int, AudioThreatSample>();
        public readonly Dictionary<long, float> Cooldowns = new Dictionary<long, float>();
        public readonly Dictionary<int, int> LastClips = new Dictionary<int, int>();
        public readonly List<long> ExpiredKeys = new List<long>();
        public readonly HashSet<int> MissingWarnings = new HashSet<int>();
        public AudioVoiceSample[] Voices;
        public System.Random CosmeticRandom;
        public float Time;
        public float ChaseHold;
        public float TensionGain;
        public float ChaseGain;
        public float DangerGain;
        public float Openness;
        public float InteriorGain;
        public float ExteriorGain;
        public float Collapse;
        public float Duck;
        public float FootstepGain = 1f;
        public Vector3 ListenerPosition;
        public bool OwnerEnabled;
        public bool MusicStarted;
        public bool Alive = true;
    }
}
