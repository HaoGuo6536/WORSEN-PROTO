// ============================================================================
// EchoSample.cs
// ============================================================================
// PURPOSE:
//   Holds one recorded player position and the observations made on that tick.
//   Sequence and recording time distinguish repeated positions without rewinding
//   the playback cursor or treating a later loop as an earlier sample.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Carry ring identity, position, room and replayable footstep/door metadata.
// DEPENDENCIES:
//   - UnityEngine position values only.
// USAGE NOTES:
//   An interpolated pending point has Complete false and emits no sample facts.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter.Archetypes.Echo
{
    public readonly struct EchoSample
    {
        public EchoSample(long sequence, double time, Vector3 position, int room, long tick,
            int footsteps = 0, int door = -1, bool complete = true)
        { Sequence = sequence; Time = time; Position = position; Room = room; Tick = tick;
            Footsteps = footsteps; Door = door; Complete = complete; }
        public long Sequence { get; }
        public double Time { get; }
        public Vector3 Position { get; }
        public int Room { get; }
        public long Tick { get; }
        public int Footsteps { get; }
        public int Door { get; }
        public bool Complete { get; }
    }
}
