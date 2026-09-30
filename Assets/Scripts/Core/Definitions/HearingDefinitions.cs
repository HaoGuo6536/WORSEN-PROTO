// ============================================================================
// HearingDefinitions.cs
// ============================================================================
// PURPOSE:
//   Defines environmental noise categories and the inputs and output of hearing.
//   The same values can feed hunter rules, hints and audio without engine queries.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Hearing contracts.
// KEY RESPONSIBILITIES:
//   - Carry validated model parameters and immutable acoustic observations.
// DEPENDENCIES:
//   - System only; no project-layer dependencies.
// USAGE NOTES:
//   Attenuations are retained-amplitude multipliers in [0,1], not loss amounts.
//   Reference distance is metres. Config owners supply all values; no tuning is
//   selected here. A default settings struct is invalid and rejected by sampling.
// ============================================================================
using System;

namespace Worsen.Core
{
    /// <summary>The environmental action that produced a noise, excluding interface and music.</summary>
    public enum NoiseSourceKind
    {
        Footstep, Landing, Slide, Vault, Rebound, Door, KnockedProp, CakePickup,
        Scream, Firecracker, Trap, Shrine, Other
    }

    /// <summary>Parameters for distance falloff, portal loss and the shared audible threshold.</summary>
    public readonly struct HearingModelSettings
    {
        public HearingModelSettings(float referenceDistance, float rolloff, float perPortalAttenuation,
            float closedDoorAttenuation, float audibleThreshold)
        {
            if (float.IsNaN(referenceDistance) || float.IsInfinity(referenceDistance) || referenceDistance <= 0f)
                throw new ArgumentOutOfRangeException(nameof(referenceDistance));
            if (float.IsNaN(rolloff) || float.IsInfinity(rolloff) || rolloff < 0f)
                throw new ArgumentOutOfRangeException(nameof(rolloff));
            if (!(perPortalAttenuation >= 0f && perPortalAttenuation <= 1f))
                throw new ArgumentOutOfRangeException(nameof(perPortalAttenuation));
            if (!(closedDoorAttenuation >= 0f && closedDoorAttenuation <= 1f))
                throw new ArgumentOutOfRangeException(nameof(closedDoorAttenuation));
            if (!(audibleThreshold >= 0f && audibleThreshold <= 1f))
                throw new ArgumentOutOfRangeException(nameof(audibleThreshold));
            ReferenceDistance = referenceDistance; Rolloff = rolloff;
            PerPortalAttenuation = perPortalAttenuation; ClosedDoorAttenuation = closedDoorAttenuation;
            AudibleThreshold = audibleThreshold;
        }
        public float ReferenceDistance { get; }
        public float Rolloff { get; }
        public float PerPortalAttenuation { get; }
        public float ClosedDoorAttenuation { get; }
        public float AudibleThreshold { get; }
    }

    /// <summary>Perceived amplitude and the selected path; portal count -1 means unreachable.</summary>
    public readonly struct HearingSample
    {
        public HearingSample(float perceivedLoudness, bool audible, int portalCount, bool crossedClosedDoor)
        {
            PerceivedLoudness = perceivedLoudness; Audible = audible;
            PortalCount = portalCount; CrossedClosedDoor = crossedClosedDoor;
        }
        public float PerceivedLoudness { get; }
        public bool Audible { get; }
        public int PortalCount { get; }
        public bool CrossedClosedDoor { get; }
    }
}
