// ============================================================================
// DefaultHunterController.cs
// ============================================================================
// PURPOSE:
//   Leaves shared Hunter decisions untouched for the five existing profiles.
//   Archetypes override only the hooks they need, while the shared controller
//   continues to own planning, attack commitment, habits and mutations.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Supply identity perception and utility filters and no movement override.
// DEPENDENCIES:
//   - Hunter-local definitions and Core sight values only.
// USAGE NOTES:
//   Stateless default module. Manager creates a separate module for each life.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Default
{
    public class DefaultHunterController : IHunterArchetypeController
    {
        public virtual bool OwnsPursuit => false;
        public virtual bool NeverLoses => false;
        public virtual IReadOnlyList<Vector3> ReplayPath => null;
        public virtual void Reset(HunterArchetypeContext context) { }
        public virtual void Tick(HunterArchetypeContext context) { }
        public virtual bool FilterVisibility(bool visible, SightProbe probe, HunterArchetypeContext context) => visible;
        public virtual float GoalUtility(HunterGoal goal, float utility) => utility;
        public virtual bool TryMovement(out Vector3 target, out float speed)
        { target = default; speed = 0f; return false; }
        public virtual void CommitReplay(int reachedPoints, bool unreachable = false) { }
        public virtual bool TryTakeFact(out HunterArchetypeFact fact) { fact = default; return false; }
    }
}
