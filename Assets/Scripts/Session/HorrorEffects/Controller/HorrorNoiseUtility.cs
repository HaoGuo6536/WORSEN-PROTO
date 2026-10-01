// ============================================================================
// HorrorNoiseUtility.cs
// ============================================================================
// PURPOSE:
//   Classifies owned noise producers until Core supplies an explicit origin field.
//   Acoustic categories alone are not universal hearing authorization; this adapter
//   is only for committed item impacts and Floor's player-triggered trap facts.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Session · HorrorEffects.
// KEY RESPONSIBILITIES:
//   - Mark firecrackers and player-triggered cake traps as authorized stimuli.
//   - Keep Pacification, ambient traps and other world sounds presentation-only.
// DEPENDENCIES:
//   - Core NoiseEvent values only; no engine calls or global state.
// USAGE NOTES:
//   WP-I must move origin into Core and filter Run/Director ingress too. This local
//   adapter must not be used to promote arbitrary world/false-positive sounds.
// ============================================================================
using Worsen.Core;
namespace Worsen.Session.HorrorEffects
{
    public static class HorrorNoiseUtility
    {
        public static HorrorNoiseOrigin Origin(NoiseEvent noise)
        {
            if (noise.SourceKind == NoiseSourceKind.Firecracker) return HorrorNoiseOrigin.Firecracker;
            if (noise.SourceKind == NoiseSourceKind.Trap && noise.Source.IsValid)
                return HorrorNoiseOrigin.PlayerTriggeredCakeTrap;
            return noise.SourceKind == NoiseSourceKind.Shrine ? HorrorNoiseOrigin.Pacification : HorrorNoiseOrigin.World;
        }
        public static bool HunterAudible(NoiseEvent noise) =>
            Origin(noise) == HorrorNoiseOrigin.Firecracker || Origin(noise) == HorrorNoiseOrigin.PlayerTriggeredCakeTrap;
        public static HorrorNoiseFact Annotate(NoiseEvent noise) => new HorrorNoiseFact(noise, Origin(noise));
        public static bool HunterAudible(HorrorNoiseFact fact) =>
            fact.Origin == HorrorNoiseOrigin.Firecracker || fact.Origin == HorrorNoiseOrigin.PlayerTriggeredCakeTrap;
    }
}
