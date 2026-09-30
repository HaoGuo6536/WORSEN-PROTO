// ============================================================================
// WeaverBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Retains one Weaver's planner, warned line and unconsumed web identities.
//   Each spawn gets its own state, preventing duplicate hunters from sharing
//   cooldowns, doorway decisions or hit acceptance.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Store injected observations and bounded facts without engine operations.
// DEPENDENCIES:
//   - Hunter observations, Core Weaver facts and UnityEngine values only.
// USAGE NOTES:
//   Reset by WeaverController for every life; no independent persistence.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Weaver
{
    public sealed class WeaverBehaviorState
    {
        public HunterArchetypeContext Context;
        public WeaverObservation Observation;
        public readonly Queue<WeaverFact> Facts = new Queue<WeaverFact>();
        public readonly Dictionary<int, float> Webs = new Dictionary<int, float>();
        public readonly HashSet<int> SeededDoors = new HashSet<int>();
        public readonly HashSet<int> PassedDoors = new HashSet<int>();
        public long LastTick = -1;
        public int Serial, LastRoom;
        public WeaverAction Action;
        public Vector3 Target, Aim, Origin, PreviousPosition;
        public float WarningRemaining, WarningDuration, Radius, Cooldown, SkitterRemaining;
        public bool Warning, Fire, Hold, Ceiling = true;
    }
}
