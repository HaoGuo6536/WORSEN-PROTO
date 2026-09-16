// ============================================================================
// HunterDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries Hunter-local sensing, action and attack values as plain contracts.
//   These definitions contain no engine operations or mutable runtime authority.
//   Cross-system feedback uses the separate Core event contracts.
// ARCHITECTURAL ROLE:
//   Definitions (section 5) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Keep per-life state separate from shared configuration and foreign systems.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   System-local values only; constructors assign data without behavior.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter
{
    public enum HunterAction { Patrol, InvestigateHint, Chase, Lunge, SearchLastKnown, CutOff, InvestigateLight, AvoidLight, FlankLight }
    public enum HunterAttackStyle { Lunge, Projectile, GroundSpikes }
    public enum HunterLightResponse { Investigate, Avoid, Flank }
    [System.Flags]
    public enum HunterWorldFacts : ulong
    {
        PlayerVisible = 1, PlayerHeard = 2, HasBelief = 4, BeliefFresh = 8,
        InLungeRange = 16, LoopDetected = 32, HasHint = 64,
        CaughtPlayer = 128, LocatedPlayer = 256, Patrolled = 512, LightObserved = 1024, DirectlyIlluminated = 2048,
        LightMemoryFresh = 4096, LightReactionReady = 8192, EscapedBeam = 16384
    }
    public readonly struct HunterLightObservation
    {
        public HunterLightObservation(bool observed, bool illuminated, Vector3 position, long tick)
        { Observed = observed; Illuminated = illuminated; Position = position; Tick = tick; }
        public bool Observed { get; }
        public bool Illuminated { get; }
        public Vector3 Position { get; }
        public long Tick { get; }
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
