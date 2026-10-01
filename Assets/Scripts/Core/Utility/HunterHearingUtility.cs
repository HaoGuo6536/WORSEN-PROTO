// ============================================================================
// HunterHearingUtility.cs
// ============================================================================
// PURPOSE:
//   Separates gameplay hearing admission from acoustic playback. Loudness, source
//   identity and sound kind cannot grant a world or presentation sound AI access.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Core · shared hearing policy.
// KEY RESPONSIBILITIES:
//   - Admit only the three owner-approved gameplay origins, failing closed otherwise.
// DEPENDENCIES:
//   - Core noise definitions only.
// USAGE NOTES:
//   Apply at every Director and direct Hunter ingress, before acoustic attenuation.
//   Producers explicitly stamp provenance; Unspecified is not a compatibility bypass.
// ============================================================================
namespace Worsen.Core
{
    public static class HunterHearingUtility
    {
        public static bool Allows(NoiseEvent noise) => noise.Origin == NoiseOrigin.PlayerMovement ||
            noise.Origin == NoiseOrigin.Firecracker || noise.Origin == NoiseOrigin.PlayerTriggeredCakeTrap;
    }
}
