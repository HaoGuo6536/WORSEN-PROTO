// ============================================================================
// RamBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Stores one Ram's attack commitment and queued facts independently of its asset.
//   Movement is accounted only after the engine confirms the swept displacement.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Hunter Ram.
// KEY RESPONSIBILITIES:
//   - Retain phase, fixed direction, remaining travel and one-hit admission.
// DEPENDENCIES:
//   - Hunter context, Core facts and UnityEngine values only.
// USAGE NOTES:
//   Reset for each spawn; no engine calls, events or shared static data.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Ram
{
    public sealed class RamBehaviorState
    {
        public HunterArchetypeContext Context;
        public RamPhase Phase;
        public Vector3 Direction, Motion;
        public float RemainingSeconds, RemainingMeters, StrideMeters;
        public bool Hit, Second, PendingMotion;
        public long LastTick = -1;
        public readonly Queue<RamFact> Facts = new Queue<RamFact>();
    }
}
