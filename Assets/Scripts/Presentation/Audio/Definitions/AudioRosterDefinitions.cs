// ============================================================================
// AudioRosterDefinitions.cs
// ============================================================================
// PURPOSE:
//   Describes named hunter cues without adding or renumbering Core's legacy ids.
//   A binding borrows an existing bank or holds an explicit no-clip placeholder.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Presentation · Audio.
// KEY RESPONSIBILITIES:
//   - Carry budget slots, exact-timing commands and designer sound-zone presets.
// DEPENDENCIES:
//   - Core cue slots and passive Unity clip/value types only.
// USAGE NOTES:
//   Named cues always enter the existing voice pool. They never create extra voices.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Presentation.Audio
{
    [Serializable]
    public struct AudioRosterBinding
    {
        public string Id;
        public CueId Bank;
        public bool Placeholder;
        public AudioClip Clip;
        public AudioRosterBinding(string id, CueId bank, bool placeholder = false)
        { Id = id; Bank = bank; Placeholder = placeholder; Clip = null; }
    }
    [Serializable]
    public struct AudioSoundZone
    {
        public string Id, Theme, Family;
        [Range(10f, 22000f)] public float Cutoff;
        public AudioSoundZone(string id, string theme, float cutoff, string family = null)
        { Id = id; Theme = theme; Family = family; Cutoff = cutoff; }
    }
    public struct AudioRosterCommand
    {
        public string Id;
        public EntityId Hunter;
        public HunterCueSlot Slot;
        public Vector3 Position;
        public float Gain, Pitch, Interval;
        public bool Exact, Stop;
    }
}
