// ============================================================================
// EchoSample.cs
// ============================================================================
// PURPOSE:
//   Holds one recorded player pose and the footsteps observed on that tick.
//   Sequence and recording time distinguish repeated positions without rewinding
//   the playback cursor or treating a later loop as an earlier sample.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Carry ring identity, recording time, body pose and footstep metadata.
// DEPENDENCIES:
//   - UnityEngine position values only.
// USAGE NOTES:
//   Only completed samples emit footsteps; interpolated poses have no new facts.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter.Archetypes.Echo
{
    public readonly struct EchoSample
    {
        public EchoSample(long sequence, double time, Vector3 position, float headingDegrees, long tick, int footsteps = 0)
        { Sequence = sequence; Time = time; Position = position; HeadingDegrees = headingDegrees;
            Tick = tick; Footsteps = footsteps; }
        public long Sequence { get; }
        public double Time { get; }
        public Vector3 Position { get; }
        public float HeadingDegrees { get; }
        public long Tick { get; }
        public int Footsteps { get; }

    }
}
