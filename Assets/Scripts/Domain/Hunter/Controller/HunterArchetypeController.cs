// ============================================================================
// HunterArchetypeController.cs
// ============================================================================
// PURPOSE:
//   Supplies neutral implementations of the shared archetype rule hooks.
//   Specialised controllers inherit this parent-owned baseline rather than
//   depending on the Default archetype folder or inheriting its legacy traits.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve shared visibility, goal utility and movement by default.
//   - Provide inert reset, replay and fact hooks for optional specialisations.
// DEPENDENCIES:
//   - Hunter Definitions and Core value types only.
// USAGE NOTES:
//   Stateless; no engine calls or random draws. Only the exact neutral baseline
//   admits legacy traits, never a derived plug-in unless it explicitly opts in.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Hunter
{
    public class HunterArchetypeController : IHunterArchetypeController, IHunterLegacyRules
    {
        public virtual bool AllowsLegacyTraits => GetType() == typeof(HunterArchetypeController);
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
