// ============================================================================
// EchoBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Stores one Echo's bounded recording and monotonic playback cursor.
//   Each tick produces authoritative kinematic poses and delayed footstep facts.
//   Presence and contact admission are independent of shared attack state.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Keep ring slots, cursor, pose commands and fact queue independent per body.
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
        internal long First, Next, Cursor, LastTick = -1, LastNoiseTick = -1, LastContactTick = -1;
        internal double Now, PlaybackTime;
        internal bool Active, Started, LookBack, Overrun;
        internal HunterReplayPose Pose;
        internal HunterArchetypeContext Context;
        internal readonly List<HunterReplayPose> Motion = new List<HunterReplayPose>();
        internal readonly Queue<HunterArchetypeFact> Facts = new Queue<HunterArchetypeFact>();
        public int RecordedCount => (int)(Next - First);
        public long ConsumedSequence => Cursor;
        public double RecordedTime => Now;
        public double ReplayedTime => PlaybackTime;
    }
}
