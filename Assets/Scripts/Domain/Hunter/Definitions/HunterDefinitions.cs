// ============================================================================
// HunterDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries local hunter attack and navigation decisions as plain values. These types do not cross the Hunter system boundary or perform engine work.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Keep authored data and system-local value contracts separate from execution.
// DEPENDENCIES:
//   - The owning Hunter system and pure UnityEngine values only.
// USAGE NOTES:
//   Scene-owned instances receive immutable shared configuration. Runtime code never changes assets.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter
{
    public enum HunterAction { Patrol, InvestigateHint, Chase, Lunge, SearchLastKnown, CutOff }
    [System.Flags]
    public enum HunterWorldFacts : ulong
    {
        PlayerVisible = 1, PlayerHeard = 2, HasBelief = 4, BeliefFresh = 8,
        InLungeRange = 16, LoopDetected = 32, HasHint = 64,
        CaughtPlayer = 128, LocatedPlayer = 256, Patrolled = 512
    }
    public enum HunterLungePhase { None, Windup, Active, Recovery }
    public readonly struct HunterTickResult
    {
        public HunterTickResult(Vector3 target, float speed, HunterLungePhase phase, Vector3 lungeDirection, bool beginLunge, bool activeContact)
        { Target = target; Speed = speed; Phase = phase; LungeDirection = lungeDirection; BeginLunge = beginLunge; ActiveContact = activeContact; }
        public Vector3 Target { get; }
        public float Speed { get; }
        public HunterLungePhase Phase { get; }
        public Vector3 LungeDirection { get; }
        public bool BeginLunge { get; }
        public bool ActiveContact { get; }
    }
}
