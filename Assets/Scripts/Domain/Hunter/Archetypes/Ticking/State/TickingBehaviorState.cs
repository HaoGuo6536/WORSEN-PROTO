// ============================================================================
// TickingBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Stores one clock's spring, key cycle and pending facts independently of assets.
//   Only TickingController changes these values; duplicate clocks never share them.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Hunter archetype state.
// KEY RESPONSIBILITIES:
//   - Hold injected context, monotonic timers and consumable key identity.
// DEPENDENCIES:
//   - Hunter context, local sound facts, Core noise and UnityEngine values.
// USAGE NOTES:
//   Reset starts a fresh full spring and an empty key slot. No events or engine work.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Ticking
{
    public sealed class TickingBehaviorState
    {
        internal HunterArchetypeContext Context;
        internal double Now, NextTickAt, NextKeyAt;
        internal double Charge;
        internal long LastTick = -1;
        internal float FollowAngle, FollowRadius;
        internal bool FollowProbed, HasFollowTarget;
        internal Vector3 FollowTarget;
        internal bool HalfWound, HasKey;
        internal int KeySerial;
        internal Vector3 KeyPosition;
        internal readonly Queue<TickingSoundFact> Sounds = new Queue<TickingSoundFact>();
        internal readonly Queue<NoiseEvent> Noises = new Queue<NoiseEvent>();
    }
}
