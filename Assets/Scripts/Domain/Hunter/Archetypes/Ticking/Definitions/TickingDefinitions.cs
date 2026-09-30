// ============================================================================
// TickingDefinitions.cs
// ============================================================================
// PURPOSE:
//   Defines local spring facts and an optional, archetype-neutral dormancy gate.
//   Public routing uses Core values and primitives, not these internal records.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter archetype contracts.
// KEY RESPONSIBILITIES:
//   - Describe sound transitions without playing audio or performing engine work.
// DEPENDENCIES:
//   - UnityEngine.Vector3 as a value only.
// USAGE NOTES:
//   The shared controller tests the interface, never a concrete Ticking class.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter.Archetypes.Ticking
{
    public interface IHunterDormancyRules { bool Dormant { get; } }
    public enum TickingSound { Tick, Winding, Stop, Wake, KeyAppeared }
    public readonly struct TickingSoundFact
    {
        public TickingSoundFact(TickingSound sound, Vector3 position, float interval, long tick)
        { Sound = sound; Position = position; Interval = interval; Tick = tick; }
        public TickingSound Sound { get; }
        public Vector3 Position { get; }
        public float Interval { get; }
        public long Tick { get; }
    }
}
