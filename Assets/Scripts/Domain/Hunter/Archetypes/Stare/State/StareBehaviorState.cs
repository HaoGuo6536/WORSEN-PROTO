// ============================================================================
// StareBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Stores one Stare's encounter, gaze hold and logical absence cadence.
//   Keeping placement confirmation separate prevents a failed engine probe from
//   spending the player's attention window on an invisible threat.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Hold camera evidence, phase timers and queued Core facts per duplicate.
// DEPENDENCIES:
//   - Hunter context, Core values and System collections only.
// USAGE NOTES:
//   The controller owns all transitions; no events or engine calls live here.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Stare
{
    public sealed class StareBehaviorState
    {
        internal HunterArchetypeContext Context;
        internal HunterPlayerView View;
        internal bool Clear, Present, Hunting, Pending = true, Hidden, Looking, HalfCalled, ForceSight, Fresh, PlacementValid;
        internal float Window, Elapsed, Held, ReturnRemaining, Side, Wander, WanderRemaining;
        internal Vector3 Position;
        internal long LastTick = -1;
        internal readonly Queue<StareFact> Facts = new Queue<StareFact>();
    }
}
