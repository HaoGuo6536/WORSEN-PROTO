// ============================================================================
// BlinderFactDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries Blinder blindness, sound and floor-trap policy across layer boundaries.
//   Floor retains trap placement/contact ownership; presentation receives durations
//   rather than depending on Hunter controllers or mutable effect state.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Blinder contracts.
// KEY RESPONSIBILITIES:
//   - Identify duplicate hunters and distinguish projectiles from floor traps.
//   - Carry Muffled Dark intent and silent-trap policy without applying effects.
// DEPENDENCIES:
//   - Core entity/noise values and UnityEngine position values only.
// USAGE NOTES:
//   Duration includes Longer Dark, not Mirror Skin (the effect receiver owns it).
//   Trap policy is a snapshot, not an instruction to create a second trap system.
// ============================================================================
using UnityEngine;
namespace Worsen.Core
{
    public enum BlinderSound { Presence, Detection, Chase, ThrowHiss, TrapTick, Catch }
    public readonly struct BlinderThrowFact
    {
        public BlinderThrowFact(EntityId hunter, long tick, int serial, Vector3 origin, Vector3 target, float radius, float speed, float range)
        { Hunter = hunter; Tick = tick; Serial = serial; Origin = origin; Target = target; Radius = radius; Speed = speed; Range = range; }
        public EntityId Hunter { get; }
        public long Tick { get; }
        public int Serial { get; }
        public Vector3 Origin { get; }
        public Vector3 Target { get; }
        public float Radius { get; }
        public float Speed { get; }
        public float Range { get; }
    }
    public readonly struct BlinderHitFact
    {
        public BlinderHitFact(EntityId hunter, EntityId player, long tick, int serial, float duration, bool muffledDark, bool trap = false)
        { Hunter = hunter; Player = player; Tick = tick; Serial = serial; Duration = duration; MuffledDark = muffledDark; Trap = trap; }
        public EntityId Hunter { get; }
        public EntityId Player { get; }
        public long Tick { get; }
        public int Serial { get; }
        public float Duration { get; }
        public bool MuffledDark { get; }
        public bool Trap { get; }
    }
    public readonly struct BlinderTrapPolicyFact
    {
        public BlinderTrapPolicyFact(EntityId hunter, long tick, int moreTrapsStacks, bool silentTraps, float duration, bool muffledDark)
        { Hunter = hunter; Tick = tick; MoreTrapsStacks = moreTrapsStacks; SilentTraps = silentTraps; Duration = duration; MuffledDark = muffledDark; }
        public EntityId Hunter { get; }
        public long Tick { get; }
        public int MoreTrapsStacks { get; }
        public bool SilentTraps { get; }
        public float Duration { get; }
        public bool MuffledDark { get; }
    }
    public readonly struct BlinderSoundFact
    {
        public BlinderSoundFact(EntityId hunter, BlinderSound sound, Vector3 position, long tick, float duration, NoiseEvent noise)
        { Hunter = hunter; Sound = sound; Position = position; Tick = tick; Duration = duration; Noise = noise; }
        public EntityId Hunter { get; }
        public BlinderSound Sound { get; }
        public Vector3 Position { get; }
        public long Tick { get; }
        public float Duration { get; }
        public NoiseEvent Noise { get; }
    }
}
