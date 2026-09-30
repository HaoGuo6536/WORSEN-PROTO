// ============================================================================
// ProgressionEffectsSnapshot.cs
// ============================================================================
// PURPOSE:
//   Pairs a legacy display snapshot with the immutable catalogue loadout it describes.
//   This additive Session read-only view leaves coordinator-owned Core contracts intact.
// ARCHITECTURAL ROLE:
//   Definitions (§5), read-only state view (§2c) · Session · Progression.
// KEY RESPONSIBILITIES:
//   - Keep generation/revision identity and active stacks together for consumers.
// DEPENDENCIES:
//   - Core progression and effect snapshots only.
// USAGE NOTES:
//   Constructed by Progression; published events use its two existing Core values.
//   Default snapshots contain an empty effects set, never a mutable state reference.
// ============================================================================
using Worsen.Core;

namespace Worsen.Session.Progression
{
    public readonly struct ProgressionEffectsSnapshot
    {
        private readonly ActiveEffects _activeEffects;
        public ProgressionEffectsSnapshot(ProgressionSnapshot progression, ActiveEffects activeEffects)
        { Progression = progression; _activeEffects = activeEffects; }
        public ProgressionSnapshot Progression { get; }
        public IReadOnlyActiveEffects ActiveEffects => _activeEffects;
    }
}
