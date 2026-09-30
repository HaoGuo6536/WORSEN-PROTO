// ============================================================================
// AudioCueCatalogueDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries the local mapping from serialized legacy cue ids to Core budget slots.
//   A world entry explicitly declares its hearing counterpart rather than inferring it from spatial playback.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Presentation · Audio.
// KEY RESPONSIBILITIES:
//   - Describe category, slot, hearing parity and protected timing without engine work.
// DEPENDENCIES:
//   - Core cue and hearing contracts only.
// USAGE NOTES:
//   Slot is interpreted by Category. False positives borrow world banks but are
//   presentation-only requests, not additional environmental catalogue entries.
// ============================================================================
using Worsen.Core;

namespace Worsen.Presentation.Audio
{
    public readonly struct AudioCueCatalogueEntry
    {
        public AudioCueCatalogueEntry(CueCategory category, int slot, NoiseSourceKind? noise = null,
            bool protectedVoice = false, bool timingIsTell = false)
        { Category = category; Slot = slot; Noise = noise; Protected = protectedVoice; TimingIsTell = timingIsTell; }
        public CueCategory Category { get; }
        public int Slot { get; }
        public NoiseSourceKind? Noise { get; }
        public bool Protected { get; }
        public bool TimingIsTell { get; }
    }
}
