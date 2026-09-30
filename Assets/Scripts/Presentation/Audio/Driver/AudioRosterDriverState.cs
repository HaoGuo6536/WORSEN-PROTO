// ============================================================================
// AudioRosterDriverState.cs
// ============================================================================
// PURPOSE:
//   Holds presentation-only roster deduplication and sound-zone assignments.
//   Mutation event receipts survive floor replacement, while hunter facts do not.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Audio.
// KEY RESPONSIBILITIES:
//   - Keep hunter/tick receipts, pending tells and room presets outside routing code.
// DEPENDENCIES:
//   - Core identities/facts, Audio commands and plain collections only.
// USAGE NOTES:
//   Owned by AudioSoundscapeDriver; Reset distinguishes a floor from a new run.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Presentation.Audio
{
    public sealed class AudioRosterDriverState
    {
        public readonly Dictionary<(EntityId, string), long> Ticks = new Dictionary<(EntityId, string), long>();
        public readonly Dictionary<EntityId, string> Archetypes = new Dictionary<EntityId, string>();
        public readonly Dictionary<EntityId, float> TickIntervals = new Dictionary<EntityId, float>();
        public readonly HashSet<int> MutationSequences = new HashSet<int>();
        public readonly HashSet<string> Missing = new HashSet<string>();
        public readonly List<AudioRosterCommand> PendingTells = new List<AudioRosterCommand>();
        public readonly Dictionary<int, string> RoomZones = new Dictionary<int, string>();
        public string DefaultZone;
        public EntityId LastAttacker;
    }
}
