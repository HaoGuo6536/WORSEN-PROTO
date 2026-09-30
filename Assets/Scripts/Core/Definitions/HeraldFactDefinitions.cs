// ============================================================================
// HeraldFactDefinitions.cs
// ============================================================================
// PURPOSE:
//   Describes the Herald's fixed screams and radius hits without audio dependencies.
//   Acoustic source and floor-wide player clue are separate so spatial audio stays
//   at the Herald while Director delivery points other hunters toward the player.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Herald contracts.
// KEY RESPONSIBILITIES:
//   - Preserve fixed attack pitch, sound identity and floor-wide noise attribution.
//   - Carry low damage, deafness duration and Deaf Landing intent in one hit.
// DEPENDENCIES:
//   - Core noise/entity types and UnityEngine position values only.
// USAGE NOTES:
//   Route FloorWideHint through DirectorManager.HearFloorWideNoise exactly once;
//   do not also enqueue Noise for gameplay hearing. Noise is the acoustic origin.
//   Both noises retain the Herald identity: duplicates cannot collapse into one
//   player-sourced noise. The existing hearing path excludes the screaming hunter.
//   Deafen duration excludes Ear Plugs, which the receiving effect owner applies.
// ============================================================================
using UnityEngine;
namespace Worsen.Core
{
    public enum HeraldSound { Discovery, ChaseOne, ChaseTwo, Attack, DrawnBreath }
    public readonly struct HeraldScreamFact
    {
        public HeraldScreamFact(EntityId hunter, HeraldSound sound, string soundId, float pitch,
            NoiseEvent noise, NoiseEvent floorWideHint, long observedTick, bool exactPosition)
        { Hunter = hunter; Sound = sound; SoundId = soundId; Pitch = pitch; Noise = noise;
            FloorWideHint = floorWideHint; ObservedTick = observedTick; ExactPosition = exactPosition; }
        public EntityId Hunter { get; }
        public HeraldSound Sound { get; }
        public string SoundId { get; }
        public float Pitch { get; }
        public NoiseEvent Noise { get; }
        public NoiseEvent FloorWideHint { get; }
        public long ObservedTick { get; }
        public bool ExactPosition { get; }
    }
    public readonly struct HeraldDeafenFact
    {
        public HeraldDeafenFact(EntityId hunter, EntityId player, long tick, Vector3 origin, float radius,
            int damage, float duration, bool preventsRunning)
        { Hunter = hunter; Player = player; Tick = tick; Origin = origin; Radius = radius;
            Damage = damage; Duration = duration; PreventsRunning = preventsRunning; }
        public EntityId Hunter { get; }
        public EntityId Player { get; }
        public long Tick { get; }
        public Vector3 Origin { get; }
        public float Radius { get; }
        public int Damage { get; }
        public float Duration { get; }
        public bool PreventsRunning { get; }
    }
    public readonly struct HeraldBreathFact
    {
        public HeraldBreathFact(EntityId hunter, Vector3 position, long tick, float duration)
        { Hunter = hunter; Position = position; Tick = tick; Duration = duration; }
        public EntityId Hunter { get; }
        public Vector3 Position { get; }
        public long Tick { get; }
        public float Duration { get; }
    }
}
