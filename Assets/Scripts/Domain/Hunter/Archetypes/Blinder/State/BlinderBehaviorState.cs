// ============================================================================
// BlinderBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Stores one Blinder's warning, projectile admissions and pending outward facts.
//   Reset clears every per-life timer and queue so duplicate spawns cannot share
//   a throw serial or replay an old floor policy.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Retain data only; controller decisions and Manager publication stay outside.
// DEPENDENCIES:
//   - Hunter context, existing sweep values and Core facts.
// USAGE NOTES:
//   Owned by one BlinderController; no events or engine operations.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter.Archetypes.Weaver;
namespace Worsen.Domain.Hunter.Archetypes.Blinder
{
    public sealed class BlinderBehaviorState
    {
        public HunterArchetypeContext Context;
        public IReadOnlyActiveEffects Effects;
        public WeaverObservation Observation;
        public long LastTick = -1;
        public BlinderAction Action;
        public Vector3 Origin, Aim, Target;
        public bool Warning, Fire, WasVisible, CatchPublished, PolicyPublished;
        public float WarningRemaining, Cooldown, SoundRemaining;
        public int Serial;
        public BlinderTrapPolicyFact Policy;
        public readonly Dictionary<int, float> Projectiles = new Dictionary<int, float>();
        public readonly Queue<BlinderSoundFact> Sounds = new Queue<BlinderSoundFact>();
        public readonly Queue<BlinderTrapPolicyFact> Policies = new Queue<BlinderTrapPolicyFact>();
        public readonly Queue<BlinderThrowFact> Throws = new Queue<BlinderThrowFact>();
    }
}
