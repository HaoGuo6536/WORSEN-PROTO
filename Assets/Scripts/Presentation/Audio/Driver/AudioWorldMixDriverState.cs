// ============================================================================
// AudioWorldMixDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains the listener's acoustic view and the clocks for silence-first mix effects.
//   Nothing here owns gameplay authority or scene objects; floor replacement discards the view.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Audio.
// KEY RESPONSIBILITIES:
//   - Store effects, embodiment, false-positive and collapse pulse observations.
// DEPENDENCIES:
//   - Core immutable level data and Audio feedback commands.
// USAGE NOTES:
//   The Driver injects time and randomness into the presenter. Full reset clears
//   everything; floor reset preserves the false-positive cooldown to avoid bursts.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Audio
{
    public sealed class AudioWorldMixDriverState
    {
        public LevelGraph Graph;
        public readonly Dictionary<int, bool> ClosedDoors = new Dictionary<int, bool>();
        public readonly Dictionary<int, RoomDestructionSample> Rooms = new Dictionary<int, RoomDestructionSample>();
        public readonly Dictionary<int, float> PulseDue = new Dictionary<int, float>();
        public readonly List<AudioFeedbackCommand> Commands = new List<AudioFeedbackCommand>();
        public Vector3 Listener;
        public bool SilentPresence, KeenEars, InChase;
        public float Closeness, DeafenedRemaining, MuffledRemaining, GraceRemaining;
        public float Time, FalsePositiveDue = -1f, HeartbeatDue, HeartbeatEnvelope;
        public float HeartbeatStrength, BreathGain, MaskGain = 1f;
        public bool Heartbeat;
    }
}
