// ============================================================================
// HunterFactDefinitions.cs
// ============================================================================
// PURPOSE:
//   Shares immutable hunter observations and mutation values across layer boundaries.
//   Session can retain accepted rules and Presentation can consume tells without
//   importing Hunter implementation types or changing shared profile assets.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Hunter contracts.
// KEY RESPONSIBILITIES:
//   - Preserve duplicate spawn identity, habit identifiers and copied replay paths.
//   - Carry accepted mutation data separately from its player-facing tell identifier.
// DEPENDENCIES:
//   - Core spawn/entity values, System collections and UnityEngine value types only.
// USAGE NOTES:
//   Mutation validation belongs to Hunter rules; these values do not select mutations.
//   Enum ordinals preserve existing serialized profile entries. Restoration is silent.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Worsen.Core
{
    public readonly struct HunterSpawnRequest
    {
        public HunterSpawnRequest(SpawnRequest spawn, int duplicateIndex)
        {
            if (duplicateIndex < 0) throw new ArgumentOutOfRangeException(nameof(duplicateIndex));
            Spawn = new SpawnRequest(spawn.ArchetypeKey, spawn.Position, spawn.Rotation, spawn.Owner, duplicateIndex);
            DuplicateIndex = duplicateIndex;
        }
        public SpawnRequest Spawn { get; }
        public int DuplicateIndex { get; }
    }
    public enum HunterArchetypeFactKind { ReplayedFootstep, ReplayedDoorPassage, TrailRevealed, ReplayTruncated, RecordingOverrun }
    public readonly struct HunterArchetypeFact
    {
        public HunterArchetypeFact(EntityId hunter, HunterArchetypeFactKind kind, Vector3 position,
            long tick, long recordedTick = -1, int objectId = -1, float gain = 1f, float pitch = 1f,
            float duration = 0f, IReadOnlyList<Vector3> path = null)
        { Hunter = hunter; Kind = kind; Position = position; Tick = tick; RecordedTick = recordedTick;
            ObjectId = objectId; Gain = gain; Pitch = pitch; Duration = duration;
            Path = path == null ? Array.Empty<Vector3>() : new List<Vector3>(path).AsReadOnly(); }
        public EntityId Hunter { get; }
        public HunterArchetypeFactKind Kind { get; }
        public Vector3 Position { get; }
        public long Tick { get; }
        public long RecordedTick { get; }
        public int ObjectId { get; }
        public float Gain { get; }
        public float Pitch { get; }
        public float Duration { get; }
        public IReadOnlyList<Vector3> Path { get; }
    }
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
        public HunterMutationFact(EntityId hunter, string archetypeKey, string tellId, long tick,
            HunterMutation? mutation = null)
        { Hunter = hunter; ArchetypeKey = archetypeKey; TellId = tellId; Tick = tick; Mutation = mutation; }
        public EntityId Hunter { get; }
        public string ArchetypeKey { get; }
        public string TellId { get; }
        public long Tick { get; }
        public HunterMutation? Mutation { get; }
    }
}
