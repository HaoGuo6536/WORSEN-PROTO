// ============================================================================
// WeaverDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries Weaver-local observations and commands for its pure decision module.
//   Outward web hits and cue facts live in Core so their consumers need no
//   dependency on Hunter implementation types.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Keep sweep evidence tick-tagged and web slow separate from damage or grabs.
// DEPENDENCIES:
//   - Core identity and UnityEngine value types only.
// USAGE NOTES:
//   Web Cutter modifies slow strength, not duration: Player applies
//   1 - (1 - SlowMultiplier) * SlowStrengthMultiplier, without disabling slide.
//   Cue facts name sound/presentation observations, never directly play assets.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Worsen.Domain.Hunter.Archetypes.Weaver
{
    public enum WeaverAction { None, Reposition, Shoot }

    public readonly struct WeaverShotSpot
    {
        public WeaverShotSpot(Vector3 position, bool reachable, bool clear)
        { Position = position; Reachable = reachable; Clear = clear; }
        public Vector3 Position { get; }
        public bool Reachable { get; }
        public bool Clear { get; }
    }
    public readonly struct WeaverObservation
    {
        public WeaverObservation(long tick, Vector3 origin, Vector3 target, float radius, bool clear,
            bool grounded, IReadOnlyList<WeaverShotSpot> spots)
        { Tick = tick; Origin = origin; Target = target; Radius = radius; Clear = clear; Grounded = grounded; Spots = spots; }
        public long Tick { get; }
        public Vector3 Origin { get; }
        public Vector3 Target { get; }
        public float Radius { get; }
        public bool Clear { get; }
        public bool Grounded { get; }
        public IReadOnlyList<WeaverShotSpot> Spots { get; }
    }

}
