// ============================================================================
// HunterRuleDefinitions.cs
// ============================================================================
// PURPOSE:
//   Describes Hunter habits and run-long rule overrides without presentation text.
//   Facts carry identities and tells so future consumers can react without exposing
//   the changed rule to the player or mutating a shared archetype asset.
// ARCHITECTURAL ROLE:
//   Definitions (section 5) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Keep habit observations, mutation commands and mutation tells immutable.
// DEPENDENCIES:
//   - Core entity identities and UnityEngine position values only.
// USAGE NOTES:
//   Hunter-local pending coordinator promotion to Core before cross-layer routing.
//   Mutations replace one value, not add or multiply it. Selection belongs to Session.
// ============================================================================
using UnityEngine;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter
{
    public enum HunterHabitKind { ThresholdPause, TurnToFace, CakeReaction }
    public enum HunterTunable
    {
        Acceleration, TurnRate, ChaseSpeedMultiplier, ActionCommitmentSeconds,
        LossSeconds, LossDistance, ThresholdPauseEnabled, TurnToFaceEnabled, CakeReactionEnabled
    }
    public readonly struct HunterHabitFact
    {
        public HunterHabitFact(EntityId hunter, HunterHabitKind kind, Vector3 position, long tick)
        { Hunter = hunter; Kind = kind; Position = position; Tick = tick; }
        public EntityId Hunter { get; }
        public HunterHabitKind Kind { get; }
        public Vector3 Position { get; }
        public long Tick { get; }
    }
    public readonly struct HunterMutation
    {
        public HunterMutation(HunterTunable tunable, float value, string tellId)
        { Tunable = tunable; Value = value; TellId = tellId; }
        public HunterTunable Tunable { get; }
        public float Value { get; }
        public string TellId { get; }
    }
    public readonly struct HunterMutationFact
    {
        public HunterMutationFact(EntityId hunter, string archetypeKey, string tellId, long tick)
        { Hunter = hunter; ArchetypeKey = archetypeKey; TellId = tellId; Tick = tick; }
        public EntityId Hunter { get; }
        public string ArchetypeKey { get; }
        public string TellId { get; }
        public long Tick { get; }
    }
}
