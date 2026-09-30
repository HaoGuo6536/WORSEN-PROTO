// ============================================================================
// EchoBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Stores one Echo's bounded recording and monotonic playback cursor.
//   Pending motion is acknowledged by the engine before its footsteps or door
//   passages become facts, so blocked motion never consumes the recording.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Keep ring slots, cursor, pending commands and fact queue independent per body.
// DEPENDENCIES:
//   - Hunter recording values; Core replay facts and UnityEngine vector values only.
// USAGE NOTES:
//   No events or logic. Reset clears floor recordings, including duplicate instances.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Echo
{
    public sealed class EchoBehaviorState
    {
        internal EchoSample[] Samples;
        internal long First, Next, Cursor, LastTick = -1, LastNoiseTick = -1;
        internal double Now, PlaybackTime;
        internal float PreviousDelay, DelayReduction;
        internal bool Attached, LookBack;
        internal Vector3 Position;
        internal HunterArchetypeContext Context;
        internal readonly List<Vector3> Motion = new List<Vector3>();
        internal readonly List<EchoSample> Pending = new List<EchoSample>();
        internal readonly Queue<HunterArchetypeFact> Facts = new Queue<HunterArchetypeFact>();
        public int RecordedCount => (int)(Next - First);
        public long ConsumedSequence => Cursor;
        public double RecordedTime => Now;
        public double ReplayedTime => PlaybackTime;
    }
}
