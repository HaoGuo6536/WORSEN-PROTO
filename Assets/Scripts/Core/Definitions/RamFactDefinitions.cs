// ============================================================================
// RamFactDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries the Ram's committed attack tells and partition impacts across layers.
//   Level remains the authority on breaking geometry; a fact never removes a wall.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · Hunter Ram contracts.
// KEY RESPONSIBILITIES:
//   - Identify the hunter, impacted object, fixed heading and sound cue.
// DEPENDENCIES:
//   - Core identity and UnityEngine value types only.
// USAGE NOTES:
//   Level supplies IRamBreakableHandle only on thin partitions and optional props.
//   Door breaking is a separate shared reaction. Object ids are floor-local.
// ============================================================================
using UnityEngine;
namespace Worsen.Core
{
    public interface IRamBreakableHandle { int RamBreakableId { get; } }
    public enum RamPhase { Ready, Windup, Charge, Stagger }
    public enum RamFactKind { Stamp, Bellow, Stride, WallStagger, PartitionImpact, Won }
    public readonly struct RamFact
    {
        public RamFact(EntityId hunter, RamFactKind kind, long tick, Vector3 position,
            Vector3 direction, string soundId, int objectId = -1)
        { Hunter = hunter; Kind = kind; Tick = tick; Position = position;
            Direction = direction; SoundId = soundId; ObjectId = objectId; }
        public EntityId Hunter { get; }
        public RamFactKind Kind { get; }
        public long Tick { get; }
        public Vector3 Position { get; }
        public Vector3 Direction { get; }
        public string SoundId { get; }
        public int ObjectId { get; }
    }
}
