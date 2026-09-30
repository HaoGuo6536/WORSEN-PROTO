// ============================================================================
// TickingFactDefinitions.cs
// ============================================================================
// PURPOSE:
//   Shares Ticking sound, threat guidance and Loud Keys observations across layers.
//   Each outward fact includes the owning hunter so duplicate clocks can retain
//   independent audio and arrows without touching the white objective channel.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Ticking contracts.
// KEY RESPONSIBILITIES:
//   - Preserve sound cadence, guidance removal and the original noise attribution.
// DEPENDENCIES:
//   - Core guidance/noise/entity values and UnityEngine position values only.
// USAGE NOTES:
//   Controllers may construct sound facts before the Manager supplies SoundId.
//   Loud Keys is floor-wide delivery, not an increase to acoustic range or loudness.
// ============================================================================
using UnityEngine;
namespace Worsen.Core
{
    public enum TickingSound { Tick, Winding, Stop, Wake, KeyAppeared }
    public readonly struct TickingSoundFact
    {
        public TickingSoundFact(TickingSound sound, Vector3 position, float interval, long tick,
            EntityId hunter = default, string soundId = null)
        { Sound = sound; Position = position; Interval = interval; Tick = tick; Hunter = hunter; SoundId = soundId; }
        public TickingSound Sound { get; }
        public Vector3 Position { get; }
        public float Interval { get; }
        public long Tick { get; }
        public EntityId Hunter { get; }
        public string SoundId { get; }
    }
    public readonly struct TickingGuidanceFact
    {
        public TickingGuidanceFact(GuidanceTarget target, bool active, long tick)
        { Target = target; Active = active; Tick = tick; }
        public GuidanceTarget Target { get; }
        public bool Active { get; }
        public long Tick { get; }
    }
    public readonly struct TickingNoiseFact
    {
        public TickingNoiseFact(EntityId hunter, NoiseEvent noise) { Hunter = hunter; Noise = noise; }
        public EntityId Hunter { get; }
        public NoiseEvent Noise { get; }
    }
}
