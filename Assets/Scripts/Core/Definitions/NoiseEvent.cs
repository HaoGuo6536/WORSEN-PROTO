// ============================================================================
// NoiseEvent.cs
// ============================================================================
//
// PURPOSE:
//   Records an audible gameplay fact without playing sound or notifying anyone.
//   Movement rules produce these records and keep them in player state, allowing
//   hunter hearing rules and recordings to examine the same past information.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared player and hunter data.
//   Despite its name this is a data record, not a C# event or a message bus.
//
// KEY RESPONSIBILITIES:
//   - Identify the entity, position, and tick that produced a noise.
//   - Carry loudness as a plain value for later hearing calculations.
//
// DEPENDENCIES:
//   - EntityId in Core and UnityEngine.Vector3 as a value type.
//
// USAGE NOTES:
//   Loudness is authored by the producing system, not calculated here. Tick
//   uses the run's fixed-step counter so replay needs no engine clock.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Core
{
    public readonly struct NoiseEvent
    {
        public NoiseEvent(EntityId source, Vector3 position, float loudness, long tick)
        {
            Source = source;
            Position = position;
            Loudness = loudness;
            Tick = tick;
        }

        public EntityId Source { get; }
        public Vector3 Position { get; }
        public float Loudness { get; }
        public long Tick { get; }
    }
}
