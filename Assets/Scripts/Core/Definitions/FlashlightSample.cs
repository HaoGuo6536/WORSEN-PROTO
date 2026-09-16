// ============================================================================
// FlashlightSample.cs
// ============================================================================
// PURPOSE:
//   Carries authoritative flashlight state and unshaken player aim for sensing and rendering.
//   Values cross system boundaries without transferring ownership of live state.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · Flashlight shared contracts.
// KEY RESPONSIBILITIES:
//   - Carry immutable observations and committed facts between layers.
//   - Keep identity and timing explicit without engine operations.
// DEPENDENCIES:
//   - Core EntityId and UnityEngine value types only.
// USAGE NOTES:
//   Positions/ranges are metres, angles are degrees, and lifetime is seconds.
//   Constructors assign data only; owning systems validate and apply rules.
// ============================================================================
using UnityEngine;

namespace Worsen.Core
{

    public readonly struct FlashlightSample
    {
        public FlashlightSample(EntityId source, long tick, bool enabled, Vector3 origin, Vector3 direction, float range, float coneDegrees)
        { Source = source; Tick = tick; Enabled = enabled; Origin = origin; Direction = direction; Range = range; ConeDegrees = coneDegrees; }
        public EntityId Source { get; }
        public long Tick { get; }
        public bool Enabled { get; }
        public Vector3 Origin { get; }
        public Vector3 Direction { get; }
        public float Range { get; }
        public float ConeDegrees { get; }
    }
}
