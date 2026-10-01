// ============================================================================
// HunterSweepDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries tick-tagged sweep evidence shared by the Weaver and Blinder.
//   Keeping these observations in the parent prevents a weapon module from
//   importing a sibling just to describe physics results.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Describe candidate firing spots and radius-matched sweep observations.
// DEPENDENCIES:
//   - UnityEngine value types and read-only collections only.
// USAGE NOTES:
//   Existing type names are retained for source compatibility. Drivers produce
//   evidence; pure archetype Controllers decide whether that evidence admits a shot.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
namespace Worsen.Domain.Hunter
{
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
