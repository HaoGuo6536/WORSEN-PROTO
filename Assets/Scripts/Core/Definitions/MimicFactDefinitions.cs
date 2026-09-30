// ============================================================================
// MimicFactDefinitions.cs
// ============================================================================
// PURPOSE:
//   Describes a false cake and its sprung bite without registering a real pickup.
//   Floor and Player consume these facts through Session, never through Hunter code.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · Hunter Mimic contracts.
// KEY RESPONSIBILITIES:
//   - Carry pose, arrow exclusion, bite hold and default-off faithless hook facts.
// DEPENDENCIES:
//   - Core entity identity and UnityEngine value types only.
// USAGE NOTES:
//   Pose never belongs to the white-arrow candidate set, even when golden.
//   FaithlessWindow is a separate temporary override requiring owner opt-in.
//   BiteStarted is a contact fact, not damage acceptance: Player grace wins.
// ============================================================================
using UnityEngine;
namespace Worsen.Core
{
    public enum MimicFactKind { Pose, PoseRemoved, BiteStarted, BiteEnded, FaithlessWindow, Population, Won }
    public readonly struct MimicFact
    {
        public MimicFact(EntityId hunter, EntityId player, MimicFactKind kind, long tick,
            Vector3 position, float seconds = 0f, bool golden = false, int extraCount = 0, string soundId = "")
        { Hunter = hunter; Player = player; Kind = kind; Tick = tick; Position = position;
            Seconds = seconds; Golden = golden; ExtraCount = extraCount; SoundId = soundId; }
        public EntityId Hunter { get; }
        public EntityId Player { get; }
        public MimicFactKind Kind { get; }
        public long Tick { get; }
        public Vector3 Position { get; }
        public float Seconds { get; }
        public bool Golden { get; }
        public int ExtraCount { get; }
        public string SoundId { get; }
        public bool WhiteArrowEligible => false;
    }
}
