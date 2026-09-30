// ============================================================================
// MimicBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Stores a single false cake's disguise and one-shot bite lifetime.
//   Posing and damage admission never mutate a shared config or real cake state.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Hunter Mimic.
// KEY RESPONSIBILITIES:
//   - Retain pose, hold timers and queued outward facts per entity life.
// DEPENDENCIES:
//   - Hunter context, Core facts and UnityEngine position values only.
// USAGE NOTES:
//   Reset per spawn. A spent Mimic cannot bite repeatedly while the player overlaps.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Mimic
{
    public sealed class MimicBehaviorState
    {
        public HunterArchetypeContext Context;
        public Vector3 Position;
        public long LastTick = -1;
        public float Hold, FaithlessElapsed, GoldenRoll;
        public bool Posed, Spent, Golden, PopulationPublished;
        public int ExtraCount;
        public readonly Queue<MimicFact> Facts = new Queue<MimicFact>();
    }
}
