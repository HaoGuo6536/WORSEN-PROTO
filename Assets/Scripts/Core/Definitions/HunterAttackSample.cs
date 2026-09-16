// ============================================================================
// HunterAttackSample.cs
// ============================================================================
// PURPOSE:
//   Carries the committed pose and timing of an enemy attack to visual feedback.
//   Presentation can draw a warning without reading an enemy Controller or mutable
//   state, keeping attack decisions independent from how the threat is shown.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Hunter attack presentation contract.
// KEY RESPONSIBILITIES:
//   - Identify the enemy, attack phase and normalized phase progress.
// DEPENDENCIES:
//   - Core EntityId and pure UnityEngine values only.
// USAGE NOTES:
//   Phase is 0 idle, 1 windup, 2 active, 3 recovery. Position is world space;
//   progress is normalized within that phase. No gameplay or engine calls.
// ============================================================================
using UnityEngine;
namespace Worsen.Core
{
    public readonly struct HunterAttackSample
    {
        public HunterAttackSample(EntityId hunter, Vector3 position, Vector3 direction, int phase, float progress)
        { Hunter = hunter; Position = position; Direction = direction; Phase = phase; Progress = progress; }
        public EntityId Hunter { get; }
        public Vector3 Position { get; }
        public Vector3 Direction { get; }
        public int Phase { get; }
        public float Progress { get; }
    }
}
